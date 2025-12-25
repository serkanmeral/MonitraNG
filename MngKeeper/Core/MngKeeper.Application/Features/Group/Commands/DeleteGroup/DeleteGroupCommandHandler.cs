using MediatR;
using MngKeeper.Application.Interfaces;
using MngKeeper.Application.Common.Helpers;
using MngKeeper.Application.Common.Exceptions;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace MngKeeper.Application.Features.Group.Commands.DeleteGroup
{
    public class DeleteGroupCommandHandler : IRequestHandler<DeleteGroupCommand, DeleteGroupResponse>
    {
        private readonly IGroupRepository _groupRepository;
        private readonly IUserRepository _userRepository;
        private readonly IDomainRepository _domainRepository;
        private readonly IKeycloakService _keycloakService;
        private readonly IMongoClient _mongoClient;
        private readonly IEventPublisher _eventPublisher;
        private readonly ILogger<DeleteGroupCommandHandler> _logger;

        public DeleteGroupCommandHandler(
            IGroupRepository groupRepository,
            IUserRepository userRepository,
            IDomainRepository domainRepository,
            IKeycloakService keycloakService,
            IMongoClient mongoClient,
            IEventPublisher eventPublisher,
            ILogger<DeleteGroupCommandHandler> logger)
        {
            _groupRepository = groupRepository;
            _userRepository = userRepository;
            _domainRepository = domainRepository;
            _keycloakService = keycloakService;
            _mongoClient = mongoClient;
            _eventPublisher = eventPublisher;
            _logger = logger;
        }

        public async Task<DeleteGroupResponse> Handle(DeleteGroupCommand request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Deleting group {GroupId} in domain {DomainId}", request.GroupId, request.DomainId);

                // Get domain to get realm name
                var domain = await _domainRepository.GetByIdAsync(request.DomainId);
                if (domain == null)
                {
                    return new DeleteGroupResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Domain not found."
                    };
                }

                // Get existing group
                var existingGroup = await _groupRepository.GetByIdAsync(request.GroupId, request.DomainId);
                if (existingGroup == null)
                {
                    return new DeleteGroupResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Group not found."
                    };
                }

                // Check if group belongs to the current domain
                if (existingGroup.DomainId != request.DomainId)
                {
                    return new DeleteGroupResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Group does not belong to the current domain."
                    };
                }

                // Check if group is a system group (admins, managers, users, guests)
                if (existingGroup.Name.ToLower() == "admins" || 
                    existingGroup.Name.ToLower() == "managers" || 
                    existingGroup.Name.ToLower() == "users" ||
                    existingGroup.Name.ToLower() == "guests")
                {
                    return new DeleteGroupResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "System groups cannot be deleted."
                    };
                }

                // Check if group has users - groups with users cannot be deleted
                var usersInGroup = await _userRepository.GetByGroupIdAsync(request.GroupId, request.DomainId);
                var userList = usersInGroup.ToList();
                if (userList.Any())
                {
                    return new DeleteGroupResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = $"Group cannot be deleted because it contains {userList.Count} user(s). Please remove all users from the group before deleting."
                    };
                }

                // Delete group from Keycloak first
                try
                {
                    var keycloakDeleted = await _keycloakService.DeleteGroupAsync(domain.RealmName, existingGroup.Name);
                    if (!keycloakDeleted)
                    {
                        _logger.LogWarning("Failed to delete group {GroupName} from Keycloak, but continuing with MongoDB deletion", existingGroup.Name);
                    }
                    else
                    {
                        _logger.LogInformation("Group {GroupName} deleted from Keycloak successfully", existingGroup.Name);
                    }
                }
                catch (Exception keycloakEx)
                {
                    // Log error but don't fail the group deletion - continue with MongoDB deletion
                    _logger.LogError(keycloakEx, "Error deleting group {GroupName} from Keycloak, but continuing with MongoDB deletion", existingGroup.Name);
                }

                // Hard delete from MongoDB (MngKeeper database)
                var deleted = await _groupRepository.DeleteAsync(request.GroupId, request.DomainId);
                if (!deleted)
                {
                    return new DeleteGroupResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Failed to delete group from database."
                    };
                }

                // Hard delete from DataGateway MongoDB (mng_{domain} database)
                try
                {
                    var database = _mongoClient.GetDatabase(domain.DatabaseName);
                    var collection = database.GetCollection<MongoDB.Bson.BsonDocument>("@groups");
                    var filter = Builders<MongoDB.Bson.BsonDocument>.Filter.Eq("__dataId", existingGroup.Id);
                    var deleteResult = await collection.DeleteOneAsync(filter);
                    
                    if (deleteResult.DeletedCount > 0)
                    {
                        _logger.LogInformation("Group hard deleted from DataGateway MongoDB: GroupId={GroupId}", existingGroup.Id);
                    }
                    else
                    {
                        _logger.LogWarning("Group not found in DataGateway MongoDB for deletion: GroupId={GroupId}", existingGroup.Id);
                    }
                }
                catch (Exception dataGatewayEx)
                {
                    // Log error but don't fail the group deletion - group is already deleted from MngKeeper DB
                    _logger.LogError(dataGatewayEx, "Failed to delete group from DataGateway MongoDB: GroupId={GroupId}", existingGroup.Id);
                }

                // Publish group deleted event (before returning success)
                var groupDeletedEvent = new GroupDeletedEvent
                {
                    GroupId = existingGroup.Id,
                    GroupName = existingGroup.Name
                };
                await EventPublishingHelper.PublishEventSafelyAsync(
                    _eventPublisher,
                    _logger,
                    groupDeletedEvent,
                    request.DomainId,
                    "GroupDeletedEvent",
                    request.GroupId);

                _logger.LogInformation("Group deleted successfully: {GroupId} in domain: {DomainId}", request.GroupId, request.DomainId);

                return new DeleteGroupResponse
                {
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                return ResponseHelper.CreateErrorResponse<DeleteGroupResponse>(
                    _logger,
                    ex,
                    "DeleteGroup",
                    request.GroupId,
                    request.DomainId);
            }
        }
    }
}
