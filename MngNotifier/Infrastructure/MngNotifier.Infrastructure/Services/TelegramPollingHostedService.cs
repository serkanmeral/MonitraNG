using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngNotifier.Application.Configuration;
using MngNotifier.Application.Models;
using MngNotifier.Application.Services;

namespace MngNotifier.Infrastructure.Services;

/// <summary>Long-poll getUpdates when Telegram is enabled and UsePolling is true.</summary>
public sealed class TelegramPollingHostedService : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<MngNotifierSettings> _settingsMonitor;
    private readonly ILogger<TelegramPollingHostedService> _logger;
    private long _offset;

    public TelegramPollingHostedService(
        IHttpClientFactory httpClientFactory,
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<MngNotifierSettings> settingsMonitor,
        ILogger<TelegramPollingHostedService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _scopeFactory = scopeFactory;
        _settingsMonitor = settingsMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Telegram polling hosted service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = _settingsMonitor.CurrentValue;
            var tg = settings.Telegram ?? new TelegramSettings();

            if (!tg.Enabled || !tg.UsePolling || string.IsNullOrWhiteSpace(tg.BotToken))
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(tg.WebhookPublicUrl))
            {
                // Prefer webhook when public URL is configured
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                continue;
            }

            try
            {
                await PollOnceAsync(tg, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Telegram getUpdates poll failed");
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }

    private async Task PollOnceAsync(TelegramSettings tg, CancellationToken stoppingToken)
    {
        var timeout = Math.Clamp(tg.PollingTimeoutSeconds, 5, 50);
        var baseUrl = (tg.ApiBaseUrl ?? "https://api.telegram.org").TrimEnd('/');
        var uri =
            $"{baseUrl}/bot{tg.BotToken.Trim()}/getUpdates?timeout={timeout}&offset={_offset}";

        var client = _httpClientFactory.CreateClient("TelegramBot");
        using var response = await client.GetAsync(uri, stoppingToken);
        var json = await response.Content.ReadAsStringAsync(stoppingToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("getUpdates HTTP {Status}: {Body}", (int)response.StatusCode, Trim(json, 200));
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            return;
        }

        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        if (!doc.RootElement.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True)
            return;

        if (!doc.RootElement.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array)
            return;

        foreach (var item in result.EnumerateArray())
        {
            var update = item.Deserialize<TelegramUpdate>(JsonOptions);
            if (update == null)
                continue;

            _offset = Math.Max(_offset, update.UpdateId + 1);

            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<ITelegramUpdateProcessor>();
            await processor.ProcessUpdateAsync(update, stoppingToken);
        }
    }

    private static string Trim(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Length <= max ? s : s[..max] + "…";
    }
}
