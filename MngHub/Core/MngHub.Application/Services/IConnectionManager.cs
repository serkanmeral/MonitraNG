using MngHub.Application.DTOs.Common;

namespace MngHub.Application.Services;

/// <summary>
/// Connection management service interface
/// </summary>
public interface IConnectionManager
{
    Task<ConnectionInfoDto> AddConnectionAsync(
        string connectionId,
        string userId,
        string domainName,
        string? notificationUserId = null);
    Task RemoveConnectionAsync(string connectionId);
    Task<ConnectionInfoDto?> GetConnectionAsync(string connectionId);
    Task<List<ConnectionInfoDto>> GetConnectionsByDomainAsync(string domainName);
    Task<List<ConnectionInfoDto>> GetAllConnectionsAsync();
    Task<bool> IsConnectedAsync(string connectionId);
    string GetDomainRoomName(string domainName);
    string GetGlobalRoomName();
    string GetUserRoomName(string userId);
}

