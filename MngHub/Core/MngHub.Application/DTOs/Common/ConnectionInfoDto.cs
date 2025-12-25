namespace MngHub.Application.DTOs.Common;

/// <summary>
/// Connection information DTO
/// </summary>
public class ConnectionInfoDto
{
    public string ConnectionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string DomainName { get; set; } = string.Empty;
    public DateTime ConnectedAt { get; set; }
    public DateTime LastActivityAt { get; set; }
    public List<string> SubscribedRoutingKeys { get; set; } = new();
    public List<string> RoomNames { get; set; } = new();
}

