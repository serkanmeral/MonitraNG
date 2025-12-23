using MediatR;
using MngKeeper.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace MngKeeper.Application.Features.User.Commands.RemoveUserFromGroup
{
    public class RemoveUserFromGroupCommandHandler : IRequestHandler<RemoveUserFromGroupCommand, RemoveUserFromGroupResponse>
    {
        private readonly IUserRepository _userRepository;
        private readonly IGroupRepository _groupRepository;
        private readonly IDomainRepository _domainRepository;
        private readonly IKeycloakService _keycloakService;
        private readonly IEventPublisher _eventPublisher;
        private readonly ILogger<RemoveUserFromGroupCommandHandler> _logger;

        public RemoveUserFromGroupCommandHandler(
            IUserRepository userRepository,
            IGroupRepository groupRepository,
            IDomainRepository domainRepository,
            IKeycloakService keycloakService,
            IEventPublisher eventPublisher,
            ILogger<RemoveUserFromGroupCommandHandler> logger)
        {
            _userRepository = userRepository;
            _groupRepository = groupRepository;
            _domainRepository = domainRepository;
            _keycloakService = keycloakService;
            _eventPublisher = eventPublisher;
            _logger = logger;
        }

        public async Task<RemoveUserFromGroupResponse> Handle(RemoveUserFromGroupCommand request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Removing user {UserId} from group {GroupId} in domain {DomainId}", 
                    request.UserId, request.GroupId, request.DomainId);

                // Get domain
                var domain = await _domainRepository.GetByIdAsync(request.DomainId);
                if (domain == null)
                {
                    return new RemoveUserFromGroupResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Domain not found."
                    };
                }

                // Get user
                var user = await _userRepository.GetByIdAsync(request.UserId, request.DomainId);
                if (user == null)
                {
                    return new RemoveUserFromGroupResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "User not found."
                    };
                }

                // Get group to get its name
                var group = await _groupRepository.GetByIdAsync(request.GroupId, request.DomainId);
                if (group == null)
                {
                    return new RemoveUserFromGroupResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Group not found."
                    };
                }

                // Check if user is in the group (by group NAME)
                if (!user.Groups.Contains(group.Name))
                {
                    return new RemoveUserFromGroupResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "User is not in the specified group."
                    };
                }

                // TODO: Remove from group in Keycloak (implement when Keycloak service supports it)
                // For now, we'll just update our database

                // Remove from user groups in database (by group NAME for consistency)
                user.Groups.Remove(group.Name);
                user.UpdatedAt = DateTime.UtcNow;
                user.UpdatedBy = MngKeeper.Application.Common.Constants.SystemConstants.SystemUser; // TODO: Get from current user context

                await _userRepository.UpdateAsync(user);

                // Publish user removed from group event (non-blocking)
                try
                {
                    var userRemovedFromGroupEvent = new UserRemovedFromGroupEvent
                    {
                        UserId = user.Id,
                        Username = user.Username,
                        GroupId = group.Id,
                        GroupName = group.Name
                    };
                    await _eventPublisher.PublishAsync(userRemovedFromGroupEvent, request.DomainId);
                }
                catch (Exception eventEx)
                {
                    // Log error but don't fail the operation
                    _logger.LogError(eventEx, "Failed to publish UserRemovedFromGroupEvent for user {UserId} and group {GroupId} in domain {DomainId}", 
                        request.UserId, request.GroupId, request.DomainId);
                }

                _logger.LogInformation("User {UserId} removed from group {GroupId} in domain {DomainId}", 
                    request.UserId, request.GroupId, request.DomainId);

                return new RemoveUserFromGroupResponse
                {
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing user {UserId} from group {GroupId} in domain {DomainId}", 
                    request.UserId, request.GroupId, request.DomainId);
                return new RemoveUserFromGroupResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Failed to remove user from group: {ex.Message}"
                };
            }
        }
    }
}
