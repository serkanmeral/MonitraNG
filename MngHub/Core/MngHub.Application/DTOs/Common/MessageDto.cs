namespace MngHub.Application.DTOs.Common;

/// <summary>
/// Message DTO for SignalR
/// </summary>
public class MessageDto
{
    public string RoutingKey { get; set; } = string.Empty;
    public object Message { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Factory method to create a MessageDto instance
    /// </summary>
    public static MessageDto Create(string routingKey, object message)
    {
        return new MessageDto
        {
            RoutingKey = routingKey,
            Message = message,
            Timestamp = DateTime.UtcNow
        };
    }
}

