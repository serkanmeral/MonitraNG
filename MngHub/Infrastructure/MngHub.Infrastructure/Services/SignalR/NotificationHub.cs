using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MngHub.Application.DTOs.Common;
using MngHub.Application.Services;
using MngHub.Domain.Constants;
using MngHub.Domain.Exceptions;
using System.Security.Claims;
using System.Text.Json;

namespace MngHub.Infrastructure.Services.SignalR;

public class NotificationHub : Hub
{
    private readonly IConnectionManager _connectionManager;
    private readonly IRabbitMqConsumer _rabbitMqConsumer;
    private readonly IJwtValidator _jwtValidator;
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(
        IConnectionManager connectionManager,
        IRabbitMqConsumer rabbitMqConsumer,
        IJwtValidator jwtValidator,
        ILogger<NotificationHub> logger)
    {
        _connectionManager = connectionManager;
        _rabbitMqConsumer = rabbitMqConsumer;
        _jwtValidator = jwtValidator;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        try
        {
            // 1. Get JWT token from query string
            var token = Context.GetHttpContext()?.Request.Query["access_token"].ToString();
            
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Connection rejected: No token provided. ConnectionId: {ConnectionId}", 
                    Context.ConnectionId);
                Context.Abort();
                return;
            }

            // 2. Validate JWT token
            var claims = await _jwtValidator.ValidateAsync(token);
            
            var domainName = claims.GetValueOrDefault("domain_name");
            var domainId = claims.GetValueOrDefault("domain_id"); // MongoDB ObjectId
            var userId = claims.GetValueOrDefault("sub") ?? claims.GetValueOrDefault(ClaimTypes.NameIdentifier);
            var username = claims.GetValueOrDefault("preferred_username") ?? claims.GetValueOrDefault("username");

            if (string.IsNullOrEmpty(domainName) || string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("Connection rejected: Invalid token claims. ConnectionId: {ConnectionId}", 
                    Context.ConnectionId);
                Context.Abort();
                return;
            }

            // 3. Register connection
            var connectionInfo = await _connectionManager.AddConnectionAsync(
                Context.ConnectionId, 
                userId, 
                domainName);

            var domainRoomName = _connectionManager.GetDomainRoomName(domainName);
            await Groups.AddToGroupAsync(Context.ConnectionId, domainRoomName);
            
            var globalRoomName = _connectionManager.GetGlobalRoomName();
            await Groups.AddToGroupAsync(Context.ConnectionId, globalRoomName);

            // Subscribe to RabbitMQ topics
            var routingKeys = new List<string>
            {
                RoutingKeyPatterns.Global,
                RoutingKeyPatterns.System,
                RoutingKeyPatterns.GetDomainPattern(domainName)
            };

            if (!string.IsNullOrEmpty(domainId))
            {
                routingKeys.Add(RoutingKeyPatterns.GetDomainPatternById(domainId));
            }

            await _rabbitMqConsumer.SubscribeAsync(
                Context.ConnectionId,
                routingKeys,
                async (routingKey, message) =>
                {
                    if (routingKey.StartsWith("global.") || routingKey.StartsWith("system."))
                    {
                        await Clients.Group(globalRoomName).SendAsync("ReceiveMessage", new MessageDto
                        {
                            RoutingKey = routingKey,
                            Message = message,
                            Timestamp = DateTime.UtcNow
                        });
                        _logger.LogInformation("Message routed to global room. RoutingKey: {RoutingKey}, Room: {Room}, ConnectionId: {ConnectionId}", 
                            routingKey, globalRoomName, Context.ConnectionId);
                    }
                    else if (routingKey.StartsWith($"domain.{domainName}."))
                    {
                        await Clients.Group(domainRoomName).SendAsync("ReceiveMessage", new MessageDto
                        {
                            RoutingKey = routingKey,
                            Message = message,
                            Timestamp = DateTime.UtcNow
                        });
                        _logger.LogDebug("Message routed to domain room {DomainRoom}. RoutingKey: {RoutingKey}", 
                            domainRoomName, routingKey);
                    }
                    else if (!string.IsNullOrEmpty(domainId) && routingKey.StartsWith($"{domainId}."))
                    {
                        await Clients.Group(domainRoomName).SendAsync("ReceiveMessage", new MessageDto
                        {
                            RoutingKey = routingKey,
                            Message = message,
                            Timestamp = DateTime.UtcNow
                        });
                        _logger.LogDebug("Message routed to domain room {DomainRoom} (by domainId). RoutingKey: {RoutingKey}", 
                            domainRoomName, routingKey);
                    }
                    else
                    {
                        _logger.LogWarning("No matching routing pattern for routing key: {RoutingKey}, Domain: {DomainName}", 
                            routingKey, domainName);
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
        await Clients.Caller.SendAsync("ReceiveMessage", new MessageDto
        {
            RoutingKey = "client.message",
            Message = message,
            Timestamp = DateTime.UtcNow
        });
    }
}

