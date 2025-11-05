using MediatR;
using MngKeeper.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace MngKeeper.Application.Features.Group.Commands.DeleteGroup
{
    public class DeleteGroupCommandHandler : IRequestHandler<DeleteGroupCommand, DeleteGroupResponse>
    {
        private readonly IGroupRepository _groupRepository;
        private readonly IDomainRepository _domainRepository;
        private readonly IKeycloakService _keycloakService;
        private readonly ILogger<DeleteGroupCommandHandler> _logger;

        public DeleteGroupCommandHandler(
            IGroupRepository groupRepository,
            IDomainRepository domainRepository,
            IKeycloakService keycloakService,
            ILogger<DeleteGroupCommandHandler> logger)
        {
            _groupRepository = groupRepository;
            _domainRepository = domainRepository;
            _keycloakService = keycloakService;
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
                var existingGroup = await _groupRepository.GetByIdAsync(request.GroupId);
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

                // Delete group from Keycloak (TODO: Implement Keycloak group deletion)
                // For now, we'll just delete from our database

                // Delete from database
                var deleted = await _groupRepository.DeleteAsync(request.GroupId);
                if (!deleted)
                {
                    return new DeleteGroupResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Failed to delete group from database."
                    };
                }

                _logger.LogInformation("Group deleted successfully: {GroupId} in domain: {DomainId}", request.GroupId, request.DomainId);

                return new DeleteGroupResponse
                {
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting group: {GroupId} in domain: {DomainId}", request.GroupId, request.DomainId);
                return new DeleteGroupResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Failed to delete group: {ex.Message}"
                };
            }
        }
    }
}
