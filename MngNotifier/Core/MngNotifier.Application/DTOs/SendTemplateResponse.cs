namespace MngNotifier.Application.DTOs;

public class SendTemplateResponse
{
    public required string NotificationId { get; set; }
    public required string Status { get; set; }
    public required string TemplateKey { get; set; }
    public DateTime QueuedAt { get; set; }
}
