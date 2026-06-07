using MngHub.Application.DTOs.Common;

namespace MngHub.Application.Services;

public interface IUserNotificationPublisher
{
    Task PublishToUserAsync(string userId, UserNotificationDto payload, CancellationToken cancellationToken = default);
}
