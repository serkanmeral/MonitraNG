using MediatR;
using MngKeeper.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace MngKeeper.Application.Features.User.Commands.AddUserToGroup
{
    public class AddUserToGroupCommandHandler : IRequestHandler<AddUserToGroupCommand, AddUserToGroupResponse>
    {
        private readonly IUserRepository _userRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly IDomainRepository _domainRepository;
        private readonly IKeycloakService _keycloakService;
        private readonly ILogger<AddUserToGroupCommandHandler> _logger;

        public AddUserToGroupCommandHandler(
            IUserRepository userRepository,
            IGroupRepository groupRepository,
            IDomainRepository domainRepository,
            IKeycloakService keycloakService,
            ILogger<AddUserToGroupCommandHandler> logger)
        {
            _userRepository = userRepository;
            _groupRepository = groupRepository;
            _domainRepository = domainRepository;
            _keycloakService = keycloakService;
            _logger = logger;
        }

        public async Task<AddUserToGroupResponse> Handle(AddUserToGroupCommand request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Adding user {UserId} to group {GroupId} in domain {DomainId}", 
                    request.UserId, request.GroupId, request.DomainId);

                // Get user
                var user = await _userRepository.GetByIdAsync(request.UserId);
                if (user == null)
                {
                    return new AddUserToGroupResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "User not found."
                    };
                }

                // Get domain
                var domain = await _domainRepository.GetByIdAsync(request.DomainId);
                if (domain == null)
                {
                    return new AddUserToGroupResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Domain not found."
                    };
                }

                // Get group to get its name for Keycloak
                var group = await _groupRepository.GetByIdAsync(request.GroupId);
                if (group == null)
                {
                    return new AddUserToGroupResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Group not found."
                    };
                }

                // Check if user is already in the group
                if (user.Groups.Contains(request.GroupId))
                {
                    return new AddUserToGroupResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "User is already in the specified group."
                    };
                }

                // Add to group in Keycloak (use Keycloak UUIDs and group name)
                var success = await _keycloakService.AddUserToGroupAsync(domain.RealmName, user.KeycloakUserId, group.Name);
                if (!success)
                {
                    return new AddUserToGroupResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Failed to add user to group in Keycloak."
                    };
                }

                // Update user groups in database (store group NAME for consistency)
                user.Groups.Add(group.Name);
                user.UpdatedAt = DateTime.UtcNow;
                user.UpdatedBy = "system"; // TODO: Get from current user context

                await _userRepository.UpdateAsync(user);

                _logger.LogInformation("User {UserId} added to group {GroupName} ({GroupId}) in domain {DomainId}", 
                    request.UserId, group.Name, request.GroupId, request.DomainId);

                return new AddUserToGroupResponse
                {
                    IsSuccess = true,
                    Username = user.Username,
                    GroupName = group.Name
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding user {UserId} to group {GroupId} in domain {DomainId}", 
                    request.UserId, request.GroupId, request.DomainId);
                return new AddUserToGroupResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Failed to add user to group: {ex.Message}"
                };
            }
        }
    }
}
