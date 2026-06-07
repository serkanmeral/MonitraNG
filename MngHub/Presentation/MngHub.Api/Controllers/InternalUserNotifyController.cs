using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MngHub.Application.Configuration;
using MngHub.Application.DTOs.Common;
using MngHub.Application.Services;

namespace MngHub.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/internal")]
[AllowAnonymous]
public class InternalUserNotifyController : ControllerBase
{
    public const string NotifyApiKeyHeaderName = "X-Monitra-Notify-Key";

    private readonly IUserNotificationPublisher _publisher;
    private readonly MngHubSettings _settings;
    private readonly ILogger<InternalUserNotifyController> _logger;

    public InternalUserNotifyController(
        IUserNotificationPublisher publisher,
        IOptions<MngHubSettings> settings,
        ILogger<InternalUserNotifyController> logger)
    {
        _publisher = publisher;
        _settings = settings.Value;
        _logger = logger;
    }

    [HttpPost("user-notify")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> PublishUserNotification(
        [FromBody] PublishUserNotificationRequest request,
        CancellationToken cancellationToken)
    {
        if (!ValidateApiKey())
            return Unauthorized(new { error = "Invalid or missing notify API key" });

        if (request == null || string.IsNullOrWhiteSpace(request.UserId))
            return BadRequest(new { error = "userId is required" });

        if (request.Payload == null
            || (string.IsNullOrWhiteSpace(request.Payload.Title)
                && string.IsNullOrWhiteSpace(request.Payload.Message)))
        {
            return BadRequest(new { error = "payload.title or payload.message is required" });
        }

        await _publisher.PublishToUserAsync(request.UserId, request.Payload, cancellationToken);

        _logger.LogDebug(
            "Internal user-notify accepted for user {UserId}, type {Type}",
            request.UserId,
            request.Payload.NotificationType);

        return Accepted(new { status = "accepted" });
    }

    private bool ValidateApiKey()
    {
        var configured = _settings.InternalNotifyApiKey?.Trim();
        if (string.IsNullOrEmpty(configured))
            return true;

        if (!Request.Headers.TryGetValue(NotifyApiKeyHeaderName, out var provided))
            return false;

        return string.Equals(provided.ToString().Trim(), configured, StringComparison.Ordinal);
    }
}
