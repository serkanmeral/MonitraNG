namespace MngHub.Application.DTOs.Common;

/// <summary>
/// User-targeted in-app / toast notification payload (SignalR ReceiveUserNotification).
/// </summary>
public class UserNotificationDto
{
    public string? NotificationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? NotificationType { get; set; }
    public string? DeepLink { get; set; }
    public string? Severity { get; set; }
    public DateTime? CreatedAt { get; set; }
}
