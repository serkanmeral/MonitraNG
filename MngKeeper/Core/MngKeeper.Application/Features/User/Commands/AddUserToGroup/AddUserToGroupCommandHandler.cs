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
        private readonly IEventPublisher _eventPublisher;
        private readonly ILogger<AddUserToGroupCommandHandler> _logger;

        public AddUserToGroupCommandHandler(
            IUserRepository userRepository,
            IGroupRepository groupRepository,
            IDomainRepository domainRepository,
            IKeycloakService keycloakService,
            IEventPublisher eventPublisher,
            ILogger<AddUserToGroupCommandHandler> logger)
        {
            _userRepository = userRepository;
            _groupRepository = groupRepository;
            _domainRepository = domainRepository;
            _keycloakService = keycloakService;
            _eventPublisher = eventPublisher;
            _logger = logger;
        }

        public async Task<AddUserToGroupResponse> Handle(AddUserToGroupCommand request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Adding user {UserId} to group {GroupId} in domain {DomainId}", 
                    request.UserId, request.GroupId, request.DomainId);

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

                // Get user
                var user = await _userRepository.GetByIdAsync(request.UserId, request.DomainId);
                if (user == null)
                {
                    return new AddUserToGroupResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "User not found."
                    };
                }

                // Get group to get its name for Keycloak
                var group = await _groupRepository.GetByIdAsync(request.GroupId, request.DomainId);
                if (group == null)
                {
                    return new AddUserToGroupResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Group not found."
                    };
                }

                // Check if user is already in the group (by group name)
                if (user.Groups.Contains(group.Name))
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
                user.UpdatedBy = MngKeeper.Application.Common.Constants.SystemConstants.SystemUser; // TODO: Get from current user context

                await _userRepository.UpdateAsync(user);

                // Publish user added to group event (non-blocking)
                try
                {
                    var userAddedToGroupEvent = new UserAddedToGroupEvent
                    {
                        UserId = user.Id,
                        Username = user.Username,
                        GroupId = group.Id,
                        GroupName = group.Name
                    };
                    await _eventPublisher.PublishAsync(userAddedToGroupEvent, request.DomainId);
                }
                catch (Exception eventEx)
                {
                    // Log error but don't fail the operation
                    _logger.LogError(eventEx, "Failed to publish UserAddedToGroupEvent for user {UserId} and group {GroupId} in domain {DomainId}", 
                        request.UserId, request.GroupId, request.DomainId);
                }

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
