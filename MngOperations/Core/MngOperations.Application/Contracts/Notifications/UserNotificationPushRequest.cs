namespace MngOperations.Application.Contracts.Notifications;

public sealed class UserNotificationPushRequest
{
    public string UserId { get; set; } = string.Empty;
    public string? NotificationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? NotificationType { get; set; }
    public string? DeepLink { get; set; }
    public string? Severity { get; set; }
    public DateTime? CreatedAt { get; set; }
}
