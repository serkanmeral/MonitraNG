using MediatR;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace MngKeeper.Application.Features.Group.Commands.UpdateGroup
{
    public class UpdateGroupCommandHandler : IRequestHandler<UpdateGroupCommand, UpdateGroupResponse>
    {
        private readonly IGroupRepository _groupRepository;
        private readonly IDomainRepository _domainRepository;
        private readonly IKeycloakService _keycloakService;
        private readonly ILogger<UpdateGroupCommandHandler> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UpdateGroupCommandHandler(
            IGroupRepository groupRepository,
            IDomainRepository domainRepository,
            IKeycloakService keycloakService,
            ILogger<UpdateGroupCommandHandler> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _groupRepository = groupRepository;
            _domainRepository = domainRepository;
            _keycloakService = keycloakService;
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
                var existingGroup = await _groupRepository.GetByIdAsync(request.GroupId);
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

                // Check if new name conflicts with existing group (excluding current group)
                if (request.Name != existingGroup.Name && await _groupRepository.ExistsByNameAsync(request.Name))
                {
                    return new UpdateGroupResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = $"Group with name '{request.Name}' already exists."
                    };
                }

                // Update group in Keycloak (TODO: Implement Keycloak group update)
                // For now, we'll just update in our database

                // Update group entity
                existingGroup.Name = request.Name;
                existingGroup.Description = request.Description;
                existingGroup.Permissions = request.Permissions;
                existingGroup.IsActive = request.IsActive;
                existingGroup.UpdatedBy = "system"; // TODO: Get from current user context
                existingGroup.UpdatedAt = DateTime.UtcNow;

                // Save to database
                var updatedGroup = await _groupRepository.UpdateAsync(existingGroup);

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
                _logger.LogError(ex, "Error updating group: {GroupId} in domain: {DomainId}", request.GroupId, claims?.DomainId);
                return new UpdateGroupResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Failed to update group: {ex.Message}"
                };
            }
        }
    }
}
