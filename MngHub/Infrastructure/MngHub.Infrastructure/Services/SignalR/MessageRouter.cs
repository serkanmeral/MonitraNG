using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MngHub.Application.DTOs.Common;
using MngHub.Infrastructure.Helpers;

namespace MngHub.Infrastructure.Services.SignalR;

/// <summary>
/// Service to route messages to appropriate SignalR groups based on routing key.
/// Chat Room (F2, MVP 3A): <c>cht_messages</c> create/update unified events use the same path —
/// domain group + <see cref="NotificationHub"/> <c>ReceiveMessage</c>; client filters by
/// <c>datasetName</c> and <c>data.roomKind</c> / <c>data.roomRecordId</c>. See
/// <c>docs/content/chat_room/CHAT_ROOM_ROADMAP.md</c> §3.2b.
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
        else if (routingKey.StartsWith($"{domainName}."))
        {
            // DataGateway events use domainName as domainId (e.g., "meral.datacreatedevent")
            targetRoom = domainRoomName;
            logLevel = LogLevel.Information;
        }
        else if (routingKey.StartsWith("monitoring.data.updated."))
        {
            // Reactor ingest notify: monitoring.data.updated.{domainName} -> domain room
            targetRoom = domainRoomName;
            logLevel = LogLevel.Debug;
        }
        else if (routingKey.StartsWith("dataset.", StringComparison.OrdinalIgnoreCase))
        {
            // MngDataGateway unified payload: dataset.{datasetName}.{created|updated|deleted|restored} on monitra.data.events.{tenant}
            targetRoom = domainRoomName;
            logLevel = LogLevel.Debug;
        }
        else
        {
            _logger.LogWarning(
                "No matching routing pattern for routing key: {RoutingKey}, Domain: {DomainName}, DomainId: {DomainId}, ExpectedPattern: {ExpectedPattern}",
                routingKey, domainName, domainId ?? "N/A", !string.IsNullOrEmpty(domainId) ? $"{domainId}.*" : "N/A");
            return;
        }

        var payload = HubPayloadNormalizer.NormalizeForClient(message);
        var messageDto = MessageDto.Create(routingKey, payload);
        await _hubContext.Clients.Group(targetRoom).SendAsync("ReceiveMessage", messageDto);

        _logger.Log(
            logLevel,
            "Message routed to {Room}. RoutingKey: {RoutingKey}, ConnectionId: {ConnectionId}",
            targetRoom, routingKey, connectionId);
    }
}

