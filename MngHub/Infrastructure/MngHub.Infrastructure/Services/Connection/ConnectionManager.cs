using Microsoft.Extensions.Logging;
using MngHub.Application.DTOs.Common;
using MngHub.Application.Services;
using MngHub.Domain.Constants;
using ConnectionEntity = MngHub.Domain.Entities.Connection;

namespace MngHub.Infrastructure.Services.Connection;

public class ConnectionManager : IConnectionManager
{
    private readonly Dictionary<string, ConnectionEntity> _connections = new();
    private readonly object _lockObject = new();
    private readonly ILogger<ConnectionManager> _logger;

    public ConnectionManager(ILogger<ConnectionManager> logger)
    {
        _logger = logger;
    }

    public Task<ConnectionInfoDto> AddConnectionAsync(
        string connectionId,
        string userId,
        string domainName,
        string? notificationUserId = null)
    {
        lock (_lockObject)
        {
            if (_connections.ContainsKey(connectionId))
            {
                _logger.LogWarning("Connection {ConnectionId} already exists", connectionId);
                var existing = _connections[connectionId];
                return Task.FromResult(MapToDto(existing));
            }

            var domainRoomName = GetDomainRoomName(domainName);
            var globalRoomName = GetGlobalRoomName();
            var roomNames = new List<string> { domainRoomName, globalRoomName };
            if (!string.IsNullOrWhiteSpace(notificationUserId))
                roomNames.Add(GetUserRoomName(notificationUserId));

            var connection = new ConnectionEntity
            {
                ConnectionId = connectionId,
                UserId = userId,
                DomainName = domainName,
                ConnectedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
                RoomNames = roomNames,
                SubscribedRoutingKeys = new List<string>
                {
                    RoutingKeyPatterns.Global,
                    RoutingKeyPatterns.GetDomainPattern(domainName)
                }
            };

            _connections[connectionId] = connection;

            _logger.LogInformation(
                "Connection added. ConnectionId: {ConnectionId}, UserId: {UserId}, Domain: {Domain}",
                connectionId, userId, domainName);

            return Task.FromResult(MapToDto(connection));
        }
    }

    public Task RemoveConnectionAsync(string connectionId)
    {
        lock (_lockObject)
        {
            if (_connections.Remove(connectionId))
            {
                _logger.LogInformation("Connection removed. ConnectionId: {ConnectionId}", connectionId);
            }
        }

        return Task.CompletedTask;
    }

    public Task<ConnectionInfoDto?> GetConnectionAsync(string connectionId)
    {
        lock (_lockObject)
        {
            if (_connections.TryGetValue(connectionId, out var connection))
            {
                return Task.FromResult<ConnectionInfoDto?>(MapToDto(connection));
            }

            return Task.FromResult<ConnectionInfoDto?>(null);
        }
    }

    public Task<List<ConnectionInfoDto>> GetConnectionsByDomainAsync(string domainName)
    {
        lock (_lockObject)
        {
            var connections = _connections.Values
                .Where(c => c.DomainName == domainName)
                .Select(MapToDto)
                .ToList();

            return Task.FromResult(connections);
        }
    }

    public Task<List<ConnectionInfoDto>> GetAllConnectionsAsync()
    {
        lock (_lockObject)
        {
            var connections = _connections.Values
                .Select(MapToDto)
                .ToList();

            return Task.FromResult(connections);
        }
    }

    public Task<bool> IsConnectedAsync(string connectionId)
    {
        lock (_lockObject)
        {
            return Task.FromResult(_connections.ContainsKey(connectionId));
        }
    }

    public string GetDomainRoomName(string domainName)
    {
        return RoomNames.GetDomainRoom(domainName);
    }

    public string GetGlobalRoomName()
    {
        return RoomNames.Global;
    }

    public string GetUserRoomName(string userId)
    {
        return RoomNames.GetUserRoom(userId);
    }

    private static ConnectionInfoDto MapToDto(ConnectionEntity connection)
    {
        return new ConnectionInfoDto
        {
            ConnectionId = connection.ConnectionId,
            UserId = connection.UserId,
            Username = connection.Username,
            DomainName = connection.DomainName,
            ConnectedAt = connection.ConnectedAt,
            LastActivityAt = connection.LastActivityAt,
            SubscribedRoutingKeys = connection.SubscribedRoutingKeys.ToList(),
            RoomNames = connection.RoomNames.ToList()
        };
    }
}

