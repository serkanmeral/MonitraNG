using MediatR;
using MngKeeper.Application.Directory;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;
using MngKeeper.Application.Common.Helpers;
using MngKeeper.Application.Common.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace MngKeeper.Application.Features.Group.Commands.UpdateGroup
{
    public class UpdateGroupCommandHandler : IRequestHandler<UpdateGroupCommand, UpdateGroupResponse>
    {
        private readonly IGroupRepository _groupRepository;
        private readonly IDomainRepository _domainRepository;
        private readonly IUserRepository _userRepository;
        private readonly IKeycloakService _keycloakService;
        private readonly IDataGatewaySyncService _dataGatewaySyncService;
        private readonly IEventPublisher _eventPublisher;
        private readonly IDirectoryCache _directoryCache;
        private readonly ILogger<UpdateGroupCommandHandler> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UpdateGroupCommandHandler(
            IGroupRepository groupRepository,
            IDomainRepository domainRepository,
            IUserRepository userRepository,
            IKeycloakService keycloakService,
            IDataGatewaySyncService dataGatewaySyncService,
            IEventPublisher eventPublisher,
            IDirectoryCache directoryCache,
            ILogger<UpdateGroupCommandHandler> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _groupRepository = groupRepository;
            _domainRepository = domainRepository;
            _userRepository = userRepository;
            _keycloakService = keycloakService;
            _dataGatewaySyncService = dataGatewaySyncService;
            _eventPublisher = eventPublisher;
            _directoryCache = directoryCache;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<UpdateGroupResponse> Handle(UpdateGroupCommand request, CancellationToken cancellationToken)
        {
            TokenClaims? claims = null;
            try
            {
                _logger.LogInformation("Updating group: {GroupId}", request.GroupId);

                // Get domain from token claims
                claims = _httpContextAccessor.HttpContext?.Items["TokenClaims"] as TokenClaims;
                
                if (claims?.DomainId == null)
                {
                    return new UpdateGroupResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Domain information not found in token."
                    };
                }

                // Get domain to get realm name
                var domain = await _domainRepository.GetByIdAsync(claims.DomainId);
                if (domain == null)
                {
                    return new UpdateGroupResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Domain not found."
                    };
                }

                // Get existing group
                var existingGroup = await _groupRepository.GetByIdAsync(request.GroupId, claims.DomainId);
                if (existingGroup == null)
                {
                    return new UpdateGroupResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Group not found."
                    };
                }

                // Check if group belongs to the current domain
                if (existingGroup.DomainId != claims.DomainId)
                {
                    return new UpdateGroupResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Group does not belong to the current domain."
                    };
                }

                if (!DirectoryGroupGuard.CanMutateGroup(existingGroup))
                {
                    return new UpdateGroupResponse
                    {
                        IsSuccess = false,
                        ErrorMessage =
                            $"Kurumsal gruplar uygulama üzerinden güncellenemez. ({DirectoryGroupGuard.ErrorCode})"
                    };
                }

                // Check if new name conflicts with existing group (excluding current group)
                if (request.Name != existingGroup.Name && await _groupRepository.ExistsByNameAsync(request.Name, claims.DomainId))
                {
                    return new UpdateGroupResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = $"Group with name '{request.Name}' already exists."
                    };
                }

                // Store old group name before updating (needed for user updates)
                var oldGroupName = existingGroup.Name;
                var groupNameChanged = request.Name != oldGroupName;

                // If group name changed, get users with old group name BEFORE updating the group
                List<MngKeeper.Domain.Entities.User> usersToUpdate = new List<MngKeeper.Domain.Entities.User>();
                if (groupNameChanged)
                {
                    try
                    {
                        // Get users that have the old group name in their Groups list
                        // We need to do this BEFORE updating the group name in MongoDB
                        var usersInGroup = await _userRepository.GetByGroupIdAsync(request.GroupId, claims.DomainId);
                        usersToUpdate = usersInGroup.ToList();
                        
                        _logger.LogInformation("Found {UserCount} users in group {OldName} that need to be updated", 
                            usersToUpdate.Count, oldGroupName);
                    }
                    catch (Exception userFetchEx)
                    {
                        _logger.LogError(userFetchEx, "Error fetching users for group {GroupId} before name change", request.GroupId);
                        // Continue - we'll try to update users after group update
                    }
                }

                // Update group in Keycloak first (if name changed)
                if (groupNameChanged)
                {
                    try
                    {
                        var keycloakUpdated = await _keycloakService.UpdateGroupAsync(
                            domain.RealmName, 
                            oldGroupName, 
                            request.Name);
                        
                        if (!keycloakUpdated)
                        {
                            _logger.LogWarning("Failed to update group {OldName} to {NewName} in Keycloak, but continuing with MongoDB update", 
                                oldGroupName, request.Name);
                        }
                        else
                        {
                            _logger.LogInformation("Group updated in Keycloak successfully: {OldName} to {NewName}", 
                                oldGroupName, request.Name);
                        }
                    }
                    catch (Exception keycloakEx)
                    {
                        _logger.LogError(keycloakEx, "Error updating group {OldName} to {NewName} in Keycloak, but continuing with MongoDB update", 
                            oldGroupName, request.Name);
                        // Continue - group is updated in MongoDB
                    }
                }

                // Update group entity
                existingGroup.Name = request.Name;
                existingGroup.Description = request.Description;
                existingGroup.Permissions = request.Permissions;
                existingGroup.IsActive = request.IsActive;
                existingGroup.IncludeInApplication = request.IncludeInApplication;
                existingGroup.UpdatedBy = MngKeeper.Application.Common.Constants.SystemConstants.SystemUser; // TODO: Get from current user context
                existingGroup.UpdatedAt = DateTime.UtcNow;

                // Save to database
                var updatedGroup = await _groupRepository.UpdateAsync(existingGroup);

                // MO dizin profil cache'ini geçersiz kıl (ad/aktif değişmiş olabilir; best-effort/fail-open).
                await _directoryCache.InvalidateGroupAsync(claims.DomainId, updatedGroup.Id);

                // If group name changed, update all users' Groups list to reflect the new name
                if (groupNameChanged && usersToUpdate.Any())
                {
                    try
                    {
                        _logger.LogInformation("Updating {UserCount} users' Groups list: replacing '{OldName}' with '{NewName}'", 
                            usersToUpdate.Count, oldGroupName, request.Name);
                        
                        foreach (var user in usersToUpdate)
                        {
                            // Replace old group name with new group name in user's Groups list
                            var groupIndex = user.Groups.IndexOf(oldGroupName);
                            if (groupIndex >= 0)
                            {
                                user.Groups[groupIndex] = request.Name;
                                user.UpdatedAt = DateTime.UtcNow;
                                user.UpdatedBy = MngKeeper.Application.Common.Constants.SystemConstants.SystemUser;
                                await _userRepository.UpdateAsync(user);
                            }
                        }
                        
                        _logger.LogInformation("Updated {UserCount} users' Groups list successfully", usersToUpdate.Count);
                    }
                    catch (Exception userUpdateEx)
                    {
                        // Log error but don't fail the group update
                        _logger.LogError(userUpdateEx, "Failed to update users' Groups list after group name change: {OldName} to {NewName}", 
                            oldGroupName, request.Name);
                        // Continue - group is updated, users will be updated later or manually
                    }
                }

                // Sync to DataGateway MongoDB (mng_{domain} database) with custom data
                try
                {
                    await _dataGatewaySyncService.SyncGroupToDataGatewayAsync(
                        updatedGroup, 
                        claims.DomainId,
                        request.CustomData);
                    _logger.LogInformation("Group synced to DataGateway: GroupId={GroupId}", updatedGroup.Id);
                }
                catch (Exception syncEx)
                {
                    // Log error but don't fail the group update
                    _logger.LogError(syncEx, "Failed to sync group to DataGateway MongoDB: GroupId={GroupId}", updatedGroup.Id);
                    // Continue - group is updated in MngKeeper DB
                }

                // Publish group updated event
                var groupUpdatedEvent = new GroupUpdatedEvent
                {
                    GroupId = updatedGroup.Id,
                    GroupName = updatedGroup.Name,
                    Permissions = updatedGroup.Permissions
                };
                await EventPublishingHelper.PublishEventSafelyAsync(
                    _eventPublisher,
                    _logger,
                    groupUpdatedEvent,
                    claims.DomainId,
                    "GroupUpdatedEvent",
                    request.GroupId);

                _logger.LogInformation("Group updated successfully: {GroupId} in domain: {DomainId}", request.GroupId, claims.DomainId);

                return new UpdateGroupResponse
                {
                    GroupId = updatedGroup.Id,
                    Name = updatedGroup.Name,
                    Description = updatedGroup.Description,
                    Permissions = updatedGroup.Permissions,
                    IsActive = updatedGroup.IsActive,
                    UpdatedAt = updatedGroup.UpdatedAt ?? DateTime.UtcNow,
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                return ResponseHelper.CreateErrorResponse<UpdateGroupResponse>(
                    _logger,
                    ex,
                    "UpdateGroup",
                    request.GroupId,
                    claims?.DomainId ?? "N/A");
            }
        }
    }
}
