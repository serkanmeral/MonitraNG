namespace MngNotifier.Application.DTOs;

public class SendMailResponse
{
    public string NotificationId { get; set; } = string.Empty;
    public string Status { get; set; } = "queued";
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
}
