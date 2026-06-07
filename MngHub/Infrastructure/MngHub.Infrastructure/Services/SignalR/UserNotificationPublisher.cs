using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using MngHub.Application.DTOs.Common;
using MngHub.Application.Services;

namespace MngHub.Infrastructure.Services.SignalR;

public class UserNotificationPublisher : IUserNotificationPublisher
{
    public const string ClientMethodName = "ReceiveUserNotification";

    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly IConnectionManager _connectionManager;
    private readonly ILogger<UserNotificationPublisher> _logger;

    public UserNotificationPublisher(
        IHubContext<NotificationHub> hubContext,
        IConnectionManager connectionManager,
        ILogger<UserNotificationPublisher> logger)
    {
        _hubContext = hubContext;
        _connectionManager = connectionManager;
        _logger = logger;
    }

    public async Task PublishToUserAsync(
        string userId,
        UserNotificationDto payload,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("User id is required", nameof(userId));

        if (payload == null)
            throw new ArgumentNullException(nameof(payload));

        var roomName = _connectionManager.GetUserRoomName(userId.Trim());
        await _hubContext.Clients.Group(roomName).SendAsync(ClientMethodName, payload, cancellationToken);

        _logger.LogInformation(
            "User notification published. UserId: {UserId}, Room: {Room}, Type: {Type}",
            userId,
            roomName,
            payload.NotificationType ?? "unknown");
    }
}
