using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MngHub.Application.DTOs.Common;
using MngHub.Infrastructure.Services.SignalR;

namespace MngHub.Infrastructure.Services.SignalR;

/// <summary>
/// Service to route messages to appropriate SignalR groups based on routing key
/// </summary>
public class MessageRouter
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<MessageRouter> _logger;

    public MessageRouter(
        IHubContext<NotificationHub> hubContext,
        ILogger<MessageRouter> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Routes a message to the appropriate SignalR group based on routing key
    /// </summary>
    public async Task RouteMessageAsync(
        string routingKey,
        object message,
        string domainName,
        string? domainId,
        string domainRoomName,
        string globalRoomName,
        string connectionId)
    {
        string targetRoom;
        LogLevel logLevel;

        if (routingKey.StartsWith("global.") || routingKey.StartsWith("system."))
        {
            targetRoom = globalRoomName;
            logLevel = LogLevel.Information;
        }
        else if (routingKey.StartsWith($"domain.{domainName}."))
        {
            targetRoom = domainRoomName;
            logLevel = LogLevel.Debug;
        }
        else if (!string.IsNullOrEmpty(domainId) && routingKey.StartsWith($"{domainId}."))
        {
            targetRoom = domainRoomName;
            logLevel = LogLevel.Information;
        }
        else
        {
            _logger.LogWarning(
                "No matching routing pattern for routing key: {RoutingKey}, Domain: {DomainName}, DomainId: {DomainId}, ExpectedPattern: {ExpectedPattern}",
                routingKey, domainName, domainId ?? "N/A", !string.IsNullOrEmpty(domainId) ? $"{domainId}.*" : "N/A");
            return;
        }

        var messageDto = MessageDto.Create(routingKey, message);
        await _hubContext.Clients.Group(targetRoom).SendAsync("ReceiveMessage", messageDto);

        _logger.Log(
            logLevel,
            "Message routed to {Room}. RoutingKey: {RoutingKey}, ConnectionId: {ConnectionId}",
            targetRoom, routingKey, connectionId);
    }
}

