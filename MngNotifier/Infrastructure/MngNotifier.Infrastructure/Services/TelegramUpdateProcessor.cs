using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngNotifier.Application.Configuration;
using MngNotifier.Application.Models;
using MngNotifier.Application.Services;

namespace MngNotifier.Infrastructure.Services;

public sealed class TelegramUpdateProcessor : ITelegramUpdateProcessor
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ITelegramMessageSender _messageSender;
    private readonly MngNotifierSettings _settings;
    private readonly ILogger<TelegramUpdateProcessor> _logger;

    public TelegramUpdateProcessor(
        IHttpClientFactory httpClientFactory,
        ITelegramMessageSender messageSender,
        IOptions<MngNotifierSettings> settings,
        ILogger<TelegramUpdateProcessor> logger)
    {
        _httpClientFactory = httpClientFactory;
        _messageSender = messageSender;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task ProcessUpdateAsync(TelegramUpdate update, CancellationToken cancellationToken = default)
    {
        var tg = _settings.Telegram ?? new TelegramSettings();
        if (!tg.Enabled)
            return;

        var text = update.Message?.Text?.Trim();
        if (string.IsNullOrEmpty(text))
            return;

        var chatId = update.Message?.Chat?.Id.ToString()
                     ?? update.Message?.From?.Id.ToString();
        if (string.IsNullOrEmpty(chatId))
            return;

        // /start or /start payload
        if (!text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
            return;

        var payload = string.Empty;
        var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1)
            payload = parts[1].Trim();

        var prefix = string.IsNullOrWhiteSpace(tg.LinkPayloadPrefix) ? "link_" : tg.LinkPayloadPrefix;
        if (string.IsNullOrEmpty(payload) || !payload.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            await _messageSender.SendTextAsync(
                chatId,
                "MonitraNG bildirim botu. Hesabınızı bağlamak için uygulamadaki \"Telegram'ı bağla\" bağlantısını kullanın.",
                null,
                true,
                cancellationToken);
            return;
        }

        var rest = payload[prefix.Length..];
        var ids = rest.Split('_', 2, StringSplitOptions.RemoveEmptyEntries);
        if (ids.Length != 2 || string.IsNullOrWhiteSpace(ids[0]) || string.IsNullOrWhiteSpace(ids[1]))
        {
            _logger.LogWarning("Invalid telegram link payload: {Payload}", payload);
            await _messageSender.SendTextAsync(
                chatId,
                "Bağlama bağlantısı geçersiz. MonitraNG profilinden tekrar deneyin.",
                null,
                true,
                cancellationToken);
            return;
        }

        var domainId = ids[0].Trim();
        var userId = ids[1].Trim();
        var username = update.Message?.From?.Username;

        var linked = await LinkInKeeperAsync(domainId, userId, chatId, username, cancellationToken);
        if (linked)
        {
            var display = string.IsNullOrWhiteSpace(username) ? "" : $" (@{username})";
            await _messageSender.SendTextAsync(
                chatId,
                $"MonitraNG hesabınız Telegram'a bağlandı{display}. Artık kişisel bildirimler bu sohbete gelebilir.",
                null,
                true,
                cancellationToken);
        }
        else
        {
            await _messageSender.SendTextAsync(
                chatId,
                "Bağlama başarısız. Lütfen daha sonra MonitraNG üzerinden tekrar deneyin.",
                null,
                true,
                cancellationToken);
        }
    }

    private async Task<bool> LinkInKeeperAsync(
        string domainId,
        string userId,
        string chatId,
        string? username,
        CancellationToken cancellationToken)
    {
        var tg = _settings.Telegram ?? new TelegramSettings();
        var baseUrl = (tg.KeeperBaseUrl ?? "http://mngkeeper:5001").TrimEnd('/');
        var uri = $"{baseUrl}/api/internal/telegram-link";

        try
        {
            var client = _httpClientFactory.CreateClient("MngKeeper");
            using var request = new HttpRequestMessage(HttpMethod.Post, uri);
            if (!string.IsNullOrWhiteSpace(_settings.InternalNotifyApiKey))
                request.Headers.TryAddWithoutValidation("X-Monitra-Notify-Key", _settings.InternalNotifyApiKey);

            request.Content = JsonContent.Create(new
            {
                domainId,
                userId,
                telegramChatId = chatId,
                telegramUsername = username
            });

            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Keeper telegram-link HTTP {Status}: {Body}", (int)response.StatusCode, body);
                return false;
            }

            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var linked = doc.RootElement.TryGetProperty("linked", out var p) && p.ValueKind == JsonValueKind.True
                         || doc.RootElement.TryGetProperty("Linked", out var p2) && p2.ValueKind == JsonValueKind.True;

            _logger.LogInformation(
                "Telegram link result linked={Linked} domain={Domain} user={User} chat={Chat}",
                linked, domainId, userId, chatId);
            return linked;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Keeper telegram-link failed for user {UserId}", userId);
            return false;
        }
    }
}
