using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MngHub.Application.DTOs.Common;
using MngHub.Application.Services;
using MngHub.Domain.Constants;
using MngHub.Domain.Exceptions;
using MngHub.Infrastructure.Extensions;
using MngHub.Infrastructure.Helpers;
using System.Security.Claims;
using System.Text.Json;

namespace MngHub.Infrastructure.Services.SignalR;

public class NotificationHub : Hub
{
    private readonly IConnectionManager _connectionManager;
    private readonly IRabbitMqConsumer _rabbitMqConsumer;
    private readonly IJwtValidator _jwtValidator;
    private readonly ILogger<NotificationHub> _logger;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly MessageRouter _messageRouter;

    public NotificationHub(
        IConnectionManager connectionManager,
        IRabbitMqConsumer rabbitMqConsumer,
        IJwtValidator jwtValidator,
        ILogger<NotificationHub> logger,
        IHubContext<NotificationHub> hubContext,
        MessageRouter messageRouter)
    {
        _connectionManager = connectionManager;
        _rabbitMqConsumer = rabbitMqConsumer;
        _jwtValidator = jwtValidator;
        _logger = logger;
        _hubContext = hubContext;
        _messageRouter = messageRouter;
    }

    public override async Task OnConnectedAsync()
    {
        try
        {
            // 1. Get JWT token from query string or Authorization header
            var httpContext = Context.GetHttpContext();
            var token = httpContext.ExtractJwtToken();
            
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Connection rejected: No token provided. ConnectionId: {ConnectionId}", 
                    Context.ConnectionId);
                Context.Abort();
                return;
            }

            // 2. Validate JWT token
            var claims = await _jwtValidator.ValidateAsync(token);
            
            // Extract claims using helper
            if (!ClaimsHelper.TryExtractRequiredClaims(claims, out var domainName, out var userId))
            {
                _logger.LogWarning("Connection rejected: Invalid token claims. ConnectionId: {ConnectionId}", 
                    Context.ConnectionId);
                Context.Abort();
                return;
            }

            var domainId = ClaimsHelper.GetDomainId(claims); // MongoDB ObjectId
            var username = ClaimsHelper.GetUsername(claims);

            // 3. Register connection
            var connectionId = Context.ConnectionId; // Capture connectionId before closure
            var connectionInfo = await _connectionManager.AddConnectionAsync(
                connectionId, 
                userId, 
                domainName);

            var domainRoomName = _connectionManager.GetDomainRoomName(domainName);
            await Groups.AddToGroupAsync(connectionId, domainRoomName);
            
            var globalRoomName = _connectionManager.GetGlobalRoomName();
            await Groups.AddToGroupAsync(connectionId, globalRoomName);

            // Subscribe to RabbitMQ topics using helper
            var routingKeys = RoutingKeyHelper.BuildRoutingKeysForConnection(domainName, domainId);

            // Log subscribed routing keys
            _logger.LogInformation(
                "Subscribing connection {ConnectionId} to routing keys: {RoutingKeys}, DomainId: {DomainId}, DomainName: {DomainName}",
                connectionId, string.Join(", ", routingKeys), domainId ?? "N/A", domainName);

            // Capture values for closure (avoid accessing Context/Clients after disposal)
            var capturedDomainName = domainName;
            var capturedDomainId = domainId;
            var capturedDomainRoomName = domainRoomName;
            var capturedGlobalRoomName = globalRoomName;

            await _rabbitMqConsumer.SubscribeAsync(
                connectionId,
                routingKeys,
                async (routingKey, message) =>
                {
                    try
                    {
                        // Log event information using helper
                        var messageJson = MessageSerializationHelper.Serialize(message);
                        _logger.LogInformation(
                            "[RabbitMQ Event Received] RoutingKey: {RoutingKey}, Domain: {DomainName}, DomainId: {DomainId}, Message: {Message}",
                            routingKey, capturedDomainName, capturedDomainId ?? "N/A", messageJson);

                        // Route message to appropriate SignalR group
                        await _messageRouter.RouteMessageAsync(
                            routingKey,
                            message,
                            capturedDomainName,
                            capturedDomainId,
                            capturedDomainRoomName,
                            capturedGlobalRoomName,
                            connectionId);
                    }
                    catch (ObjectDisposedException)
                    {
                        _logger.LogDebug("Hub disposed, skipping message. RoutingKey: {RoutingKey}, ConnectionId: {ConnectionId}", 
                            routingKey, connectionId);
                        // Connection is disposed, ignore the message
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing message. RoutingKey: {RoutingKey}, ConnectionId: {ConnectionId}", 
                            routingKey, connectionId);
                    }
                });

            _logger.LogInformation(
                "Client connected. ConnectionId: {ConnectionId}, UserId: {UserId}, Domain: {Domain}",
                Context.ConnectionId, userId, domainName);

            await base.OnConnectedAsync();
        }
        catch (JwtValidationException ex)
        {
            _logger.LogWarning(ex, "JWT validation failed. ConnectionId: {ConnectionId}", Context.ConnectionId);
            Context.Abort();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during connection. ConnectionId: {ConnectionId}", Context.ConnectionId);
            Context.Abort();
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        try
        {
            // Get connection info before removing
            var connectionInfo = await _connectionManager.GetConnectionAsync(Context.ConnectionId);
            
            // Unsubscribe from RabbitMQ
            await _rabbitMqConsumer.UnsubscribeAsync(Context.ConnectionId);
            
            if (connectionInfo != null)
            {
                var domainRoomName = _connectionManager.GetDomainRoomName(connectionInfo.DomainName);
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, domainRoomName);
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, _connectionManager.GetGlobalRoomName());
            }
            
            await _connectionManager.RemoveConnectionAsync(Context.ConnectionId);

            _logger.LogInformation("Client disconnected. ConnectionId: {ConnectionId}", Context.ConnectionId);

            await base.OnDisconnectedAsync(exception);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disconnection. ConnectionId: {ConnectionId}", Context.ConnectionId);
        }
    }

    public async Task SendMessage(string message)
    {
        await Clients.Caller.SendAsync("ReceiveMessage", MessageDto.Create("client.message", message));
    }
}

