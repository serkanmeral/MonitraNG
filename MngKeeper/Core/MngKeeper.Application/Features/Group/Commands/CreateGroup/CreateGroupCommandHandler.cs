using MediatR;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;
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
        private readonly ILogger<CreateGroupCommandHandler> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CreateGroupCommandHandler(
            IGroupRepository groupRepository,
            IDomainRepository domainRepository,
            IKeycloakService keycloakService,
            IEventPublisher eventPublisher,
            ILogger<CreateGroupCommandHandler> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _groupRepository = groupRepository;
            _domainRepository = domainRepository;
            _keycloakService = keycloakService;
            _eventPublisher = eventPublisher;
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
                
                if (claims?.DomainId == null)
                {
                    return new CreateGroupResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Domain information not found in token."
                    };
                }

                // Get domain to get realm name
                var domain = await _domainRepository.GetByIdAsync(claims.DomainId);
                if (domain == null)
                {
                    return new CreateGroupResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Domain not found."
                    };
                }

                // Check if group already exists
                if (await _groupRepository.ExistsByNameAsync(request.Name))
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

                var keycloakGroup = await _keycloakService.CreateGroupAsync(domain.RealmName, keycloakGroupRequest);
                if (keycloakGroup == null)
                {
                    return new CreateGroupResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Failed to create group in Keycloak."
                    };
                }

                // Create group entity
                var group = new MngKeeper.Domain.Entities.Group
                {
                    Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString(),
                    Name = request.Name,
                    Description = request.Description,
                    Permissions = request.Permissions,
                    IsActive = request.IsActive,
                    DomainId = claims.DomainId,
                    CreatedBy = "system", // TODO: Get from current user context
                    CreatedAt = DateTime.UtcNow
                };

                // Save to database
                var savedGroup = await _groupRepository.AddAsync(group);

                // Publish group created event
                var groupCreatedEvent = new GroupCreatedEvent
                {
                    GroupId = savedGroup.Id,
                    GroupName = savedGroup.Name,
                    Permissions = savedGroup.Permissions
                };
                await _eventPublisher.PublishAsync(groupCreatedEvent, claims.DomainId);

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
                _logger.LogError(ex, "Error creating group: {Name} in domain: {DomainId}", request.Name, claims?.DomainId);
                return new CreateGroupResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Failed to create group: {ex.Message}"
                };
            }
        }
    }
}
