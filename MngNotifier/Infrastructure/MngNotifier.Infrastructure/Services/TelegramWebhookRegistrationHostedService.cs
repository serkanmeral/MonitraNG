using System.Net.Http.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngNotifier.Application.Configuration;

namespace MngNotifier.Infrastructure.Services;

/// <summary>Registers or clears Telegram webhook when WebhookPublicUrl is configured.</summary>
public sealed class TelegramWebhookRegistrationHostedService : IHostedService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<MngNotifierSettings> _settings;
    private readonly ILogger<TelegramWebhookRegistrationHostedService> _logger;

    public TelegramWebhookRegistrationHostedService(
        IHttpClientFactory httpClientFactory,
        IOptions<MngNotifierSettings> settings,
        ILogger<TelegramWebhookRegistrationHostedService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var tg = _settings.Value.Telegram ?? new TelegramSettings();
        if (!tg.Enabled || string.IsNullOrWhiteSpace(tg.BotToken))
            return;

        var baseUrl = (tg.ApiBaseUrl ?? "https://api.telegram.org").TrimEnd('/');
        var client = _httpClientFactory.CreateClient("TelegramBot");

        if (!string.IsNullOrWhiteSpace(tg.WebhookPublicUrl))
        {
            var uri = $"{baseUrl}/bot{tg.BotToken.Trim()}/setWebhook";
            var payload = new Dictionary<string, object?>
            {
                ["url"] = tg.WebhookPublicUrl.Trim(),
                ["drop_pending_updates"] = false
            };
            if (!string.IsNullOrWhiteSpace(tg.WebhookSecretToken))
                payload["secret_token"] = tg.WebhookSecretToken;

            using var response = await client.PostAsJsonAsync(uri, payload, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation("Telegram setWebhook HTTP {Status}: {Body}", (int)response.StatusCode, body);
        }
        else if (tg.UsePolling)
        {
            // Ensure polling works (webhook must be empty)
            var uri = $"{baseUrl}/bot{tg.BotToken.Trim()}/deleteWebhook?drop_pending_updates=false";
            using var response = await client.GetAsync(uri, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogInformation("Telegram deleteWebhook (for polling) HTTP {Status}: {Body}", (int)response.StatusCode, body);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
