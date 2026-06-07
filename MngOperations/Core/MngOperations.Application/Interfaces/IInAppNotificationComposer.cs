using MngOperations.Application.Models;

namespace MngOperations.Application.Interfaces;

public sealed class InAppNotificationContent
{
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? ToastSeverity { get; init; }
    public string NotificationType { get; init; } = string.Empty;
}

public interface IInAppNotificationComposer
{
    Task<InAppNotificationContent> ComposeAsync(
        NotificationDispatchRequest request,
        NotificationPolicyRecord? policy,
        string? templateKeyOverride,
        CancellationToken cancellationToken = default);
}
