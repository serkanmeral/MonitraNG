namespace MngHub.Application.DTOs.Common;

public class PublishUserNotificationRequest
{
    public string UserId { get; set; } = string.Empty;
    public UserNotificationDto Payload { get; set; } = new();
}
