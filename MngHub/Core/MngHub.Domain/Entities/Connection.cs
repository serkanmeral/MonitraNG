namespace MngHub.Domain.Entities;

/// <summary>
/// WebSocket connection entity for tracking active connections
/// </summary>
public class Connection
{
    /// <summary>
    /// SignalR connection ID
    /// </summary>
    public string ConnectionId { get; set; } = string.Empty;

    /// <summary>
    /// User ID from JWT token
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Username from JWT token
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Domain name from JWT token
    /// </summary>
    public string DomainName { get; set; } = string.Empty;

    /// <summary>
    /// Connection timestamp (UTC)
    /// </summary>
    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last activity timestamp (UTC)
    /// </summary>
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Subscribed RabbitMQ routing keys
    /// </summary>
    public List<string> SubscribedRoutingKeys { get; set; } = new();

    /// <summary>
    /// SignalR room names (groups)
    /// </summary>
    public List<string> RoomNames { get; set; } = new();
}

