using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MngNotifier.Application.Configuration;
using MngNotifier.Application.DTOs;
using MngNotifier.Application.Exceptions;
using MngNotifier.Application.Models;
using MngNotifier.Application.Services;

namespace MngNotifier.Api.Controllers;

/// <summary>
/// Notification controller for sending mail notifications
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/notifications")]
[AllowAnonymous]
public class NotificationController : ControllerBase
{
    /// <summary>İç servis (DG) chat-mention çağrıları için paylaşılan anahtar başlığı.</summary>
    public const string NotifyApiKeyHeaderName = "X-Monitra-Notify-Key";

    private readonly IMailProvider _mailProvider;
    private readonly ITelegramMessageSender _telegramMessageSender;
    private readonly ITemplateRenderService _templateRenderService;
    private readonly IMessageTemplateRenderService _messageTemplateRenderService;
    private readonly ILogger<NotificationController> _logger;
    private readonly MngNotifierSettings _notifierSettings;

    public NotificationController(
        IMailProvider mailProvider,
        ITelegramMessageSender telegramMessageSender,
        ITemplateRenderService templateRenderService,
        IMessageTemplateRenderService messageTemplateRenderService,
        ILogger<NotificationController> logger,
        IOptions<MngNotifierSettings> notifierSettings)
    {
        _mailProvider = mailProvider ?? throw new ArgumentNullException(nameof(mailProvider));
        _telegramMessageSender = telegramMessageSender ?? throw new ArgumentNullException(nameof(telegramMessageSender));
        _templateRenderService = templateRenderService ?? throw new ArgumentNullException(nameof(templateRenderService));
        _messageTemplateRenderService = messageTemplateRenderService ?? throw new ArgumentNullException(nameof(messageTemplateRenderService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _notifierSettings = notifierSettings?.Value ?? throw new ArgumentNullException(nameof(notifierSettings));
    }

    /// <summary>
    /// Sends a mail notification (Direct API - No authentication required)
    /// </summary>
    [HttpPost("mail")]
    [ProducesResponseType(typeof(SendMailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SendMail([FromBody] SendMailRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (request == null)
                return BadRequest(new { error = "Request body is required" });

            if (request.To == null || request.To.Count == 0)
                return BadRequest(new { error = "At least one 'to' recipient is required" });

            if (string.IsNullOrWhiteSpace(request.Subject))
                return BadRequest(new { error = "Subject is required" });

            if (string.IsNullOrWhiteSpace(request.Body))
                return BadRequest(new { error = "Body is required" });

            var notificationId = Guid.NewGuid().ToString();
            await _mailProvider.SendMailAsync(request, cancellationToken);

            _logger.LogInformation("Mail notification sent successfully. NotificationId: {NotificationId}, To: {To}, Subject: {Subject}",
                notificationId, string.Join(", ", request.To), request.Subject);

            return Ok(new SendMailResponse
            {
                NotificationId = notificationId,
                Status = "sent",
                QueuedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send mail notification. To: {To}, Subject: {Subject}",
                request?.To != null ? string.Join(", ", request.To) : "unknown", request?.Subject ?? "unknown");

            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to send mail notification", message = ex.Message });
        }
    }

    /// <summary>
    /// Renders a DG template and sends mail (requires Bearer token for DG template read).
    /// </summary>
    [HttpPost("send-template")]
    [ProducesResponseType(typeof(SendTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendTemplate([FromBody] SendTemplateRequest? request, CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new { error = "Request body is required" });

        if (request.To == null || request.To.Count == 0)
            return BadRequest(new { error = "At least one 'to' recipient is required" });

        if (string.IsNullOrWhiteSpace(request.TemplateKey))
            return BadRequest(new { error = "TemplateKey is required" });

        if (!TryGetBearerToken(out var token))
            return Unauthorized(new { error = "Authorization Bearer token is required for template rendering" });

        try
        {
            var rendered = await _templateRenderService.RenderAsync(new TemplateRenderRequest
            {
                TemplateKey = request.TemplateKey.Trim(),
                Context = request.Context,
                SubjectOverride = request.Subject
            }, token, cancellationToken);

            await _mailProvider.SendMailAsync(new SendMailRequest
            {
                To = request.To,
                Cc = request.Cc,
                From = request.From,
                Subject = rendered.Subject,
                Body = rendered.HtmlBody,
                IsHtml = true
            }, cancellationToken);

            var notificationId = Guid.NewGuid().ToString();
            _logger.LogInformation(
                "Template mail sent. NotificationId={NotificationId} TemplateKey={TemplateKey} To={To}",
                notificationId, rendered.TemplateKey, string.Join(", ", request.To));

            return Ok(new SendTemplateResponse
            {
                NotificationId = notificationId,
                Status = "sent",
                TemplateKey = rendered.TemplateKey,
                QueuedAt = DateTime.UtcNow
            });
        }
        catch (TemplateRenderException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send template mail. TemplateKey={TemplateKey}", request.TemplateKey);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to send template mail", message = ex.Message });
        }
    }

    /// <summary>
    /// Renders template without sending (preview). Requires Bearer token for DG.
    /// </summary>
    [HttpPost("preview-template")]
    [ProducesResponseType(typeof(PreviewTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PreviewTemplate([FromBody] PreviewTemplateRequest? request, CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new { error = "Request body is required" });

        if (string.IsNullOrWhiteSpace(request.TemplateKey))
            return BadRequest(new { error = "TemplateKey is required" });

        if (!TryGetBearerToken(out var token))
            return Unauthorized(new { error = "Authorization Bearer token is required for template rendering" });

        try
        {
            var rendered = await _templateRenderService.RenderAsync(new TemplateRenderRequest
            {
                TemplateKey = request.TemplateKey.Trim(),
                Context = request.Context,
                SubjectOverride = request.Subject,
                BodyHtmlOverride = string.IsNullOrWhiteSpace(request.BodyHtmlOverride) ? null : request.BodyHtmlOverride.Trim(),
                LayoutKeyOverride = string.IsNullOrWhiteSpace(request.LayoutKeyOverride) ? null : request.LayoutKeyOverride.Trim(),
                LocaleOverride = string.IsNullOrWhiteSpace(request.LocaleOverride) ? null : request.LocaleOverride.Trim()
            }, token, cancellationToken);

            return Ok(new PreviewTemplateResponse
            {
                TemplateKey = rendered.TemplateKey,
                LayoutKey = rendered.LayoutKey,
                Subject = rendered.Subject,
                HtmlBody = rendered.HtmlBody
            });
        }
        catch (TemplateRenderException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to preview template. TemplateKey={TemplateKey}", request.TemplateKey);
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "Failed to preview template", message = ex.Message });
        }
    }

    /// <summary>
    /// Push-only channel message (Telegram MVP). One-way notify — not a chatbot.
    /// </summary>
    [HttpPost("send-message")]
    [ProducesResponseType(typeof(SendMessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest? request, CancellationToken cancellationToken)
    {
        if (request == null)
            return BadRequest(new { error = "Request body is required" });

        var channel = (request.Channel ?? "telegram").Trim().ToLowerInvariant();
        if (channel != "telegram")
            return BadRequest(new { error = $"Unsupported channel '{request.Channel}'. MVP supports: telegram" });

        var hasTemplate = !string.IsNullOrWhiteSpace(request.TemplateKey);
        var text = request.Text?.Trim() ?? string.Empty;
        var parseMode = request.ParseMode;

        if (hasTemplate)
        {
            if (!TryGetBearerToken(out var token))
                return Unauthorized(new { error = "Authorization Bearer token is required for template rendering" });

            try
            {
                var rendered = await _messageTemplateRenderService.RenderAsync(
                    new MessageTemplateRenderRequest
                    {
                        TemplateKey = request.TemplateKey!.Trim(),
                        Context = request.Context,
                        ParseModeOverride = request.ParseMode
                    },
                    token,
                    cancellationToken);

                text = rendered.Text;
                if (string.IsNullOrWhiteSpace(parseMode))
                    parseMode = rendered.ParseMode;
            }
            catch (TemplateRenderException ex)
            {
                var status = ex.StatusCode is >= 400 and < 600 ? ex.StatusCode : StatusCodes.Status400BadRequest;
                return StatusCode(status, new { error = ex.Message });
            }
        }

        if (string.IsNullOrWhiteSpace(text))
            return BadRequest(new { error = "Text is required (or provide TemplateKey + context)" });

        var tg = _notifierSettings.Telegram ?? new TelegramSettings();
        if (!tg.Enabled)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Telegram channel is disabled" });

        if (string.IsNullOrWhiteSpace(tg.BotToken))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Telegram BotToken is not configured" });

        var recipients = (request.To ?? new List<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (recipients.Count == 0 && !string.IsNullOrWhiteSpace(tg.DefaultChatId))
            recipients.Add(tg.DefaultChatId.Trim());

        if (recipients.Count == 0)
            return BadRequest(new { error = "At least one 'to' chat_id is required (or configure Telegram:DefaultChatId)" });

        var notificationId = Guid.NewGuid().ToString();
        var results = new List<SendMessageTargetResult>();
        foreach (var chatId in recipients)
        {
            var result = await _telegramMessageSender.SendTextAsync(
                chatId,
                text,
                parseMode,
                request.DisableWebPagePreview,
                cancellationToken);
            results.Add(result);
        }

        var sent = results.Count(r => r.Success);
        var failed = results.Count - sent;
        var statusLabel = failed == 0 ? "sent" : sent == 0 ? "failed" : "partial";

        _logger.LogInformation(
            "Send-message {Channel}: NotificationId={NotificationId} Status={Status} Sent={Sent} Failed={Failed} TemplateKey={TemplateKey}",
            channel, notificationId, statusLabel, sent, failed, request.TemplateKey);

        return Ok(new SendMessageResponse
        {
            NotificationId = notificationId,
            Status = statusLabel,
            Channel = channel,
            SentCount = sent,
            FailedCount = failed,
            Results = results,
            QueuedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Chat Room: mention bildirimi (MVP: yapılandırılmış log).
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

    private bool TryGetBearerToken(out string token)
    {
        token = string.Empty;
        if (!Request.Headers.TryGetValue("Authorization", out var auth) || auth.Count == 0)
            return false;

        var value = auth.ToString();
        if (!value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return false;

        token = value["Bearer ".Length..].Trim();
        return !string.IsNullOrWhiteSpace(token);
    }
}
