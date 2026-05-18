using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MngNotifier.Application.Configuration;
using MngNotifier.Application.DTOs;
using MngNotifier.Application.Services;

namespace MngNotifier.Api.Controllers;

/// <summary>
/// Notification controller for sending mail notifications
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/notifications")]
[AllowAnonymous] // Direct API endpoint - no authentication required
public class NotificationController : ControllerBase
{
    /// <summary>İç servis (DG) chat-mention çağrıları için paylaşılan anahtar başlığı.</summary>
    public const string NotifyApiKeyHeaderName = "X-Monitra-Notify-Key";

    private readonly IMailProvider _mailProvider;
    private readonly ILogger<NotificationController> _logger;
    private readonly MngNotifierSettings _notifierSettings;

    public NotificationController(
        IMailProvider mailProvider,
        ILogger<NotificationController> logger,
        IOptions<MngNotifierSettings> notifierSettings)
    {
        _mailProvider = mailProvider ?? throw new ArgumentNullException(nameof(mailProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _notifierSettings = notifierSettings?.Value ?? throw new ArgumentNullException(nameof(notifierSettings));
    }

    /// <summary>
    /// Sends a mail notification (Direct API - No authentication required)
    /// </summary>
    /// <param name="request">Mail notification request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Notification response with ID and status</returns>
    [HttpPost("mail")]
    [ProducesResponseType(typeof(SendMailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SendMail([FromBody] SendMailRequest request, CancellationToken cancellationToken)
    {
        try
        {
            // Basic validation
            if (request == null)
            {
                return BadRequest(new { error = "Request body is required" });
            }

            if (request.To == null || request.To.Count == 0)
            {
                return BadRequest(new { error = "At least one 'to' recipient is required" });
            }

            if (string.IsNullOrWhiteSpace(request.Subject))
            {
                return BadRequest(new { error = "Subject is required" });
            }

            if (string.IsNullOrWhiteSpace(request.Body))
            {
                return BadRequest(new { error = "Body is required" });
            }

            // Generate notification ID (temporary - will use MongoDB ObjectId in future)
            var notificationId = Guid.NewGuid().ToString();

            // Send mail directly (synchronous for now - will be async with RabbitMQ in future)
            await _mailProvider.SendMailAsync(request, cancellationToken);

            _logger.LogInformation("Mail notification sent successfully. NotificationId: {NotificationId}, To: {To}, Subject: {Subject}", 
                notificationId, string.Join(", ", request.To), request.Subject);

            var response = new SendMailResponse
            {
                NotificationId = notificationId,
                Status = "sent", // For direct API, mail is sent immediately
                QueuedAt = DateTime.UtcNow
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send mail notification. To: {To}, Subject: {Subject}", 
                request?.To != null ? string.Join(", ", request.To) : "unknown", request?.Subject ?? "unknown");
            
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to send mail notification", message = ex.Message });
        }
    }

    /// <summary>
    /// Chat Room: <c>cht_messages</c> kaydı oluşturulduğunda mention hedeflerine iç bildirim hattı (MVP: yapılandırılmış log; e-posta yok).
    /// MngDataGateway iç ağından çağrılır — dışa açık gateway politikası ayrıca sıkılaştırılabilir.
    /// </summary>
    [HttpPost("chat-mention")]
    [ProducesResponseType(typeof(ChatMentionNotifyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult AcceptChatMention([FromBody] ChatMentionNotifyRequest? request)
    {
        if (!string.IsNullOrWhiteSpace(_notifierSettings.InternalNotifyApiKey))
        {
            if (!Request.Headers.TryGetValue(NotifyApiKeyHeaderName, out var supplied) ||
                supplied.Count != 1 ||
                !string.Equals(supplied.ToString(), _notifierSettings.InternalNotifyApiKey, StringComparison.Ordinal))
            {
                return Unauthorized(new { error = "Invalid or missing notify API key" });
            }
        }

        if (request == null)
            return BadRequest(new { error = "Request body is required" });

        if (string.IsNullOrWhiteSpace(request.DomainName))
            return BadRequest(new { error = "DomainName is required" });

        if (string.IsNullOrWhiteSpace(request.DataId))
            return BadRequest(new { error = "DataId is required" });

        if (string.IsNullOrWhiteSpace(request.ActorPersonId))
            return BadRequest(new { error = "ActorPersonId is required" });

        if (request.TargetPersonIds == null || request.TargetPersonIds.Count == 0)
            return BadRequest(new { error = "At least one TargetPersonId is required" });

        var distinct = request.TargetPersonIds
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (distinct.Count == 0)
            return BadRequest(new { error = "TargetPersonIds contained no valid entries" });

        var notificationId = Guid.NewGuid().ToString();

        foreach (var targetId in distinct)
        {
            _logger.LogInformation(
                "Chat mention (MVP): NotificationId={NotificationId} Domain={Domain} DataId={DataId} Target={Target} Actor={Actor} Source={Source} Preview={Preview}",
                notificationId,
                request.DomainName,
                request.DataId,
                targetId,
                request.ActorPersonId,
                request.Source,
                request.BodyPreview ?? string.Empty);
        }

        return Ok(new ChatMentionNotifyResponse
        {
            NotificationId = notificationId,
            TargetCount = distinct.Count,
            Status = "accepted",
            AcceptedAt = DateTime.UtcNow
        });
    }
}
