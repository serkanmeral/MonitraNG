using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MngNotifier.Application.Configuration;
using MngNotifier.Application.Models;
using MngNotifier.Application.Services;

namespace MngNotifier.Api.Controllers;

/// <summary>Telegram Bot webhook (prod). Same processor as long-polling.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/telegram")]
[AllowAnonymous]
public class TelegramWebhookController : ControllerBase
{
    private readonly ITelegramUpdateProcessor _processor;
    private readonly MngNotifierSettings _settings;
    private readonly ILogger<TelegramWebhookController> _logger;

    public TelegramWebhookController(
        ITelegramUpdateProcessor processor,
        IOptions<MngNotifierSettings> settings,
        ILogger<TelegramWebhookController> logger)
    {
        _processor = processor;
        _settings = settings.Value;
        _logger = logger;
    }

    [HttpPost("webhook")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Webhook([FromBody] TelegramUpdate? update, CancellationToken cancellationToken)
    {
        var tg = _settings.Telegram ?? new TelegramSettings();
        if (!tg.Enabled)
            return Ok();

        if (!string.IsNullOrWhiteSpace(tg.WebhookSecretToken))
        {
            if (!Request.Headers.TryGetValue("X-Telegram-Bot-Api-Secret-Token", out var secret) ||
                !string.Equals(secret.ToString(), tg.WebhookSecretToken, StringComparison.Ordinal))
            {
                _logger.LogWarning("Telegram webhook rejected: invalid secret token");
                return Unauthorized();
            }
        }

        if (update != null)
            await _processor.ProcessUpdateAsync(update, cancellationToken);

        return Ok();
    }
}
