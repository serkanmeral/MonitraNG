namespace MngNotifier.Application.DTOs;

public class ChatMentionNotifyResponse
{
    public string NotificationId { get; set; } = string.Empty;
    public int TargetCount { get; set; }
    public string Status { get; set; } = "accepted";
    public DateTime AcceptedAt { get; set; } = DateTime.UtcNow;
}
