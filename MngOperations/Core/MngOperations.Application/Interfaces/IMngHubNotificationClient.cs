using MngOperations.Application.Contracts.Notifications;

namespace MngOperations.Application.Interfaces;

public interface IMngHubNotificationClient
{
    Task PushUserNotificationAsync(
        UserNotificationPushRequest request,
        CancellationToken cancellationToken = default);
}
