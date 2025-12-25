using MediatR;
using MngKeeper.Application.Interfaces;
using MngKeeper.Application.Common.Helpers;
using MngKeeper.Application.Common.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using MongoDB.Driver;

namespace MngKeeper.Application.Features.User.Commands.DeleteUser
{
    public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, DeleteUserResponse>
    {
        private readonly IUserRepository _userRepository;
        private readonly IDomainRepository _domainRepository;
        private readonly IKeycloakService _keycloakService;
        private readonly IDataGatewaySyncService _dataGatewaySyncService;
        private readonly IMongoClient _mongoClient;
        private readonly IEventPublisher _eventPublisher;
        private readonly ILogger<DeleteUserCommandHandler> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public DeleteUserCommandHandler(
            IUserRepository userRepository,
            IDomainRepository domainRepository,
            IKeycloakService keycloakService,
            IDataGatewaySyncService dataGatewaySyncService,
            IMongoClient mongoClient,
            IEventPublisher eventPublisher,
            ILogger<DeleteUserCommandHandler> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _userRepository = userRepository;
            _domainRepository = domainRepository;
            _keycloakService = keycloakService;
            _dataGatewaySyncService = dataGatewaySyncService;
            _mongoClient = mongoClient;
            _eventPublisher = eventPublisher;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<DeleteUserResponse> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
        {
            TokenClaims? claims = null;
            try
            {
                _logger.LogInformation("Deleting user: {UserId}", request.UserId);

                // Get domain from token claims
                claims = _httpContextAccessor.HttpContext?.Items["TokenClaims"] as TokenClaims;
                
                if (claims?.DomainId == null)
                {
                    return new DeleteUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Domain information not found in token."
                    };
                }

                // Get domain to get realm name
                var domain = await _domainRepository.GetByIdAsync(claims.DomainId);
                if (domain == null)
                {
                    return new DeleteUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Domain not found."
                    };
                }

                // Get existing user
                var existingUser = await _userRepository.GetByIdAsync(request.UserId, claims.DomainId);
                if (existingUser == null)
                {
                    return new DeleteUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "User not found."
                    };
                }

                // Check if user belongs to the current domain
                if (existingUser.DomainId != claims.DomainId)
                {
                    return new DeleteUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "User does not belong to the current domain."
                    };
                }

                // Check if user is a system admin user (domain admin)
                if (existingUser.Username.EndsWith("_admin"))
                {
                    return new DeleteUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "System admin users cannot be deleted."
                    };
                }

                // Delete user from Keycloak (TODO: Implement Keycloak user deletion)
                // For now, we'll just delete from our database

                // Delete from database
                var deleted = await _userRepository.DeleteAsync(request.UserId, claims.DomainId);
                if (!deleted)
                {
                    return new DeleteUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Failed to delete user from database."
                    };
                }

                // Sync soft delete to DataGateway MongoDB (mng_{domain} database)
                // Set IsDeleted = true in DataGateway
                try
                {
                    // Get user before deletion for sync (or use existingUser)
                    existingUser.IsActive = false; // Mark as inactive
                    await _dataGatewaySyncService.SyncUserToDataGatewayAsync(
                        existingUser, 
                        claims.DomainId,
                        null);
                    
                    // Update IsDeleted flag in DataGateway
                    var domainForSync = await _domainRepository.GetByIdAsync(claims.DomainId);
                    if (domainForSync != null)
                    {
                        var database = _mongoClient.GetDatabase(domainForSync.DatabaseName);
                        var collection = database.GetCollection<MongoDB.Bson.BsonDocument>("@users");
                        var filter = Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("__dataId", existingUser.Id);
                        var update = Builders<MongoDB.Bson.BsonDocument>.Update
                            .Set("__isDeleted", true)
                            .Set("__lastUpdateInfo.updatedAt", DateTime.UtcNow);
                        await collection.UpdateOneAsync(filter, update);
                    }
                    
                    _logger.LogInformation("User soft deleted in DataGateway: UserId={UserId}", existingUser.Id);
                }
                catch (Exception syncEx)
                {
                    // Log error but don't fail the user deletion
                    _logger.LogError(syncEx, "Failed to sync user deletion to DataGateway MongoDB: UserId={UserId}", existingUser.Id);
                    // Continue - user is deleted from MngKeeper DB
                }

                // Publish user deleted event
                var userDeletedEvent = new UserDeletedEvent
                {
                    UserId = existingUser.Id,
                    Username = existingUser.Username
                };
                await EventPublishingHelper.PublishEventSafelyAsync(
                    _eventPublisher,
                    _logger,
                    userDeletedEvent,
                    claims.DomainId,
                    "UserDeletedEvent",
                    request.UserId);

                _logger.LogInformation("User deleted successfully: {UserId} in domain: {DomainId}", request.UserId, claims.DomainId);

                return new DeleteUserResponse
                {
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                return ResponseHelper.CreateErrorResponse<DeleteUserResponse>(
                    _logger,
                    ex,
                    "DeleteUser",
                    request.UserId,
                    claims?.DomainId ?? "N/A");
            }
        }
    }
}
