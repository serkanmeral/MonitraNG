using MediatR;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;
using MngKeeper.Application.Common.Helpers;
using MngKeeper.Application.Common.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace MngKeeper.Application.Features.User.Commands.UpdateUser
{
    public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, UpdateUserResponse>
    {
        private readonly IUserRepository _userRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly IDomainRepository _domainRepository;
        private readonly IKeycloakService _keycloakService;
        private readonly IDataGatewaySyncService _dataGatewaySyncService;
        private readonly IEventPublisher _eventPublisher;
        private readonly ILogger<UpdateUserCommandHandler> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UpdateUserCommandHandler(
            IUserRepository userRepository,
            IGroupRepository groupRepository,
            IDomainRepository domainRepository,
            IKeycloakService keycloakService,
            IDataGatewaySyncService dataGatewaySyncService,
            IEventPublisher eventPublisher,
            ILogger<UpdateUserCommandHandler> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _userRepository = userRepository;
            _groupRepository = groupRepository;
            _domainRepository = domainRepository;
            _keycloakService = keycloakService;
            _dataGatewaySyncService = dataGatewaySyncService;
            _eventPublisher = eventPublisher;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<UpdateUserResponse> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
        {
            TokenClaims? claims = null;
            try
            {
                _logger.LogInformation("Updating user: {UserId}", request.UserId);

                // Get domain from token claims
                claims = _httpContextAccessor.HttpContext?.Items["TokenClaims"] as TokenClaims;
                
                if (claims?.DomainId == null)
                {
                    return new UpdateUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Domain information not found in token."
                    };
                }

                // Get domain to get realm name
                var domain = await _domainRepository.GetByIdAsync(claims.DomainId);
                if (domain == null)
                {
                    return new UpdateUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Domain not found."
                    };
                }

                // Get existing user
                var existingUser = await _userRepository.GetByIdAsync(request.UserId, claims.DomainId);
                if (existingUser == null)
                {
                    return new UpdateUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "User not found."
                    };
                }

                // Check if user belongs to the current domain
                if (existingUser.DomainId != claims.DomainId)
                {
                    return new UpdateUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "User does not belong to the current domain."
                    };
                }

                // Check if new email conflicts with existing user (excluding current user)
                if (request.Email != existingUser.Email && await _userRepository.ExistsByEmailAsync(request.Email, claims.DomainId))
                {
                    return new UpdateUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = $"User with email '{request.Email}' already exists."
                    };
                }

                // Check if new username conflicts with existing user (excluding current user)
                if (request.Username != existingUser.Username && await _userRepository.ExistsByUsernameAsync(request.Username, claims.DomainId))
                {
                    return new UpdateUserResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = $"User with username '{request.Username}' already exists."
                    };
                }

                // Update user in Keycloak (TODO: Implement Keycloak user update)
                // For now, we'll just update in our database

                // Update user entity
                existingUser.Username = request.Username;
                existingUser.Email = request.Email;
                existingUser.FirstName = request.FirstName;
                existingUser.LastName = request.LastName;
                existingUser.Title = request.Title;
                existingUser.Department = request.Department;
                existingUser.Gender = request.Gender;
                existingUser.PhoneNumber = request.PhoneNumber;
                existingUser.PhotoUrl = request.PhotoUrl;
                
                // Convert group IDs to group names (User.Groups stores group names, not IDs)
                if (request.GroupIds != null && request.GroupIds.Any())
                {
                    var groupNames = new List<string>();
                    foreach (var groupId in request.GroupIds)
                    {
                        var group = await _groupRepository.GetByIdAsync(groupId, claims.DomainId);
                        if (group != null && group.DomainId == claims.DomainId)
                        {
                            groupNames.Add(group.Name);
                        }
                    }
                    existingUser.Groups = groupNames;
                }
                else
                {
                    existingUser.Groups = new List<string>();
                }
                
                existingUser.IsActive = request.IsActive;
                existingUser.UpdatedBy = MngKeeper.Application.Common.Constants.SystemConstants.SystemUser; // TODO: Get from current user context
                existingUser.UpdatedAt = DateTime.UtcNow;

                // Save to database
                var updatedUser = await _userRepository.UpdateAsync(existingUser);

                // Sync to DataGateway MongoDB (mng_{domain} database) with custom data
                try
                {
                    await _dataGatewaySyncService.SyncUserToDataGatewayAsync(
                        updatedUser, 
                        claims.DomainId,
                        request.CustomData);
                    _logger.LogInformation("User synced to DataGateway: UserId={UserId}", updatedUser.Id);
                }
                catch (Exception syncEx)
                {
                    // Log error but don't fail the user update
                    _logger.LogError(syncEx, "Failed to sync user to DataGateway MongoDB: UserId={UserId}", updatedUser.Id);
                    // Continue - user is updated in MngKeeper DB
                }

                // Publish user updated event
                var userUpdatedEvent = new UserUpdatedEvent
                {
                    UserId = updatedUser.Id,
                    Username = updatedUser.Username,
                    Email = updatedUser.Email,
                    Groups = updatedUser.Groups
                };
                await EventPublishingHelper.PublishEventSafelyAsync(
                    _eventPublisher,
                    _logger,
                    userUpdatedEvent,
                    claims.DomainId,
                    "UserUpdatedEvent",
                    request.UserId);

                _logger.LogInformation("User updated successfully: {UserId} in domain: {DomainId}", request.UserId, claims.DomainId);

                return new UpdateUserResponse
                {
                    UserId = updatedUser.Id,
                    Username = updatedUser.Username,
                    Email = updatedUser.Email,
                    FirstName = updatedUser.FirstName,
                    LastName = updatedUser.LastName,
                    Title = updatedUser.Title,
                    Department = updatedUser.Department,
                    Gender = updatedUser.Gender,
                    PhoneNumber = updatedUser.PhoneNumber,
                    PhotoUrl = updatedUser.PhotoUrl,
                    GroupIds = updatedUser.Groups,
                    IsActive = updatedUser.IsActive,
                    UpdatedAt = updatedUser.UpdatedAt ?? DateTime.UtcNow,
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                return ResponseHelper.CreateErrorResponse<UpdateUserResponse>(
                    _logger,
                    ex,
                    "UpdateUser",
                    request.UserId,
                    claims?.DomainId ?? "N/A");
            }
        }
    }
}
