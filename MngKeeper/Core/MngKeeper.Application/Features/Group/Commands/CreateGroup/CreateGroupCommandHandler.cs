using MediatR;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Enums;
using MngKeeper.Domain.Entities;
using MngKeeper.Application.Common.Helpers;
using MngKeeper.Application.Common.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace MngKeeper.Application.Features.Group.Commands.CreateGroup
{
    public class CreateGroupCommandHandler : IRequestHandler<CreateGroupCommand, CreateGroupResponse>
    {
        private readonly IGroupRepository _groupRepository;
        private readonly IDomainRepository _domainRepository;
        private readonly IKeycloakService _keycloakService;
        private readonly IEventPublisher _eventPublisher;
        private readonly IDataGatewaySyncService _dataGatewaySyncService;
        private readonly ILogger<CreateGroupCommandHandler> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CreateGroupCommandHandler(
            IGroupRepository groupRepository,
            IDomainRepository domainRepository,
            IKeycloakService keycloakService,
            IEventPublisher eventPublisher,
            IDataGatewaySyncService dataGatewaySyncService,
            ILogger<CreateGroupCommandHandler> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _groupRepository = groupRepository;
            _domainRepository = domainRepository;
            _keycloakService = keycloakService;
            _eventPublisher = eventPublisher;
            _dataGatewaySyncService = dataGatewaySyncService;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<CreateGroupResponse> Handle(CreateGroupCommand request, CancellationToken cancellationToken)
        {
            TokenClaims? claims = null;
            try
            {
                _logger.LogInformation("Creating group: {Name}", request.Name);

                // Get domain from token claims
                claims = _httpContextAccessor.HttpContext?.Items["TokenClaims"] as TokenClaims;
                
                MngKeeper.Domain.Entities.Domain? domain = null;
                
                // Try to get domain by ID first
                if (claims?.DomainId != null)
                {
                    domain = await _domainRepository.GetByIdAsync(claims.DomainId);
                }
                
                // If domain not found by ID, try to find by name
                if (domain is null && !string.IsNullOrEmpty(claims?.DomainName))
                {
                    domain = await _domainRepository.GetByNameAsync(claims.DomainName);
                    // Update claims with the found domain ID
                    if (domain is not null && claims is not null)
                    {
                        claims.DomainId = domain!.Id;
                    }
                }
                
                if (domain is null)
                {
                    return new CreateGroupResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Domain information not found in token or domain does not exist."
                    };
                }

                // At this point, domain is guaranteed to be non-null
                MngKeeper.Domain.Entities.Domain domainValue = domain!;

                // Check if group already exists
                if (await _groupRepository.ExistsByNameAsync(request.Name, claims.DomainId))
                {
                    return new CreateGroupResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = $"Group with name '{request.Name}' already exists."
                    };
                }

                // Create group in Keycloak
                var keycloakGroupRequest = new CreateGroupRequest
                {
                    Name = request.Name,
                    Description = request.Description
                };

                var keycloakGroup = await _keycloakService.CreateGroupAsync(domainValue.RealmName, keycloakGroupRequest);
                if (keycloakGroup == null)
                {
                    return new CreateGroupResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Failed to create group in Keycloak."
                    };
                }

                // Create group entity (only for sync to domain database, not saved to mngkeeper database)
                var group = new MngKeeper.Domain.Entities.Group
                {
                    Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString(),
                    Name = request.Name,
                    Description = request.Description,
                    Permissions = request.Permissions,
                    IsActive = request.IsActive,
                    DomainId = claims.DomainId,
                    KeycloakGroupId = keycloakGroup.Id ?? string.Empty,
                    ProvisioningSource = UserProvisioningSource.Local,
                    CreatedBy = MngKeeper.Application.Common.Constants.SystemConstants.SystemUser, // TODO: Get from current user context
                    CreatedAt = DateTime.UtcNow
                };

                // Save to domain-specific database (groups collection)
                var savedGroup = await _groupRepository.AddAsync(group);
                _logger.LogInformation("Group saved to domain database groups collection: GroupId={GroupId}", savedGroup.Id);

                // Sync to domain database (@groups collection for DataGateway) with custom data
                try
                {
                    await _dataGatewaySyncService.SyncGroupToDataGatewayAsync(
                        savedGroup, 
                        claims.DomainId,
                        request.CustomData);
                    _logger.LogInformation("Group synced to domain database @groups collection: GroupId={GroupId}", savedGroup.Id);
                }
                catch (Exception syncEx)
                {
                    // Log error but don't fail the group creation
                    _logger.LogError(syncEx, "Failed to sync group to domain database @groups collection: GroupId={GroupId}", savedGroup.Id);
                    // Continue - group is created in Keycloak and domain database
                }

                // Publish group created event (notification only)
                var groupCreatedEvent = new GroupCreatedEvent
                {
                    GroupId = savedGroup.Id,
                    GroupName = savedGroup.Name,
                    Permissions = savedGroup.Permissions
                };
                await EventPublishingHelper.PublishEventSafelyAsync(
                    _eventPublisher,
                    _logger,
                    groupCreatedEvent,
                    claims.DomainId,
                    "GroupCreatedEvent",
                    savedGroup.Id);

                _logger.LogInformation("Group created successfully: {Name} in domain: {DomainId}", request.Name, claims.DomainId);

                return new CreateGroupResponse
                {
                    GroupId = savedGroup.Id,
                    Name = savedGroup.Name,
                    Description = savedGroup.Description,
                    Permissions = savedGroup.Permissions,
                    IsActive = savedGroup.IsActive,
                    CreatedAt = savedGroup.CreatedAt,
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                return ResponseHelper.CreateErrorResponse<CreateGroupResponse>(
                    _logger,
                    ex,
                    "CreateGroup",
                    request.Name,
                    claims?.DomainId ?? "N/A");
            }
        }
    }
}
