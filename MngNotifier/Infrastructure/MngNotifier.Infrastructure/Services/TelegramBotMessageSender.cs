using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngNotifier.Application.Configuration;
using MngNotifier.Application.DTOs;
using MngNotifier.Application.Services;

namespace MngNotifier.Infrastructure.Services;

/// <summary>Telegram Bot API sendMessage — outbound notify only.</summary>
public sealed class TelegramBotMessageSender : ITelegramMessageSender
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MngNotifierSettings _settings;
    private readonly ILogger<TelegramBotMessageSender> _logger;

    public TelegramBotMessageSender(
        IHttpClientFactory httpClientFactory,
        IOptions<MngNotifierSettings> settings,
        ILogger<TelegramBotMessageSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<SendMessageTargetResult> SendTextAsync(
        string chatId,
        string text,
        string? parseMode,
        bool disableWebPagePreview,
        CancellationToken cancellationToken = default)
    {
        var tg = _settings.Telegram ?? new TelegramSettings();
        if (!tg.Enabled)
        {
            return new SendMessageTargetResult
            {
                To = chatId,
                Success = false,
                Error = "Telegram channel disabled"
            };
        }

        if (string.IsNullOrWhiteSpace(tg.BotToken))
        {
            return new SendMessageTargetResult
            {
                To = chatId,
                Success = false,
                Error = "Telegram BotToken not configured"
            };
        }

        if (string.IsNullOrWhiteSpace(chatId))
        {
            return new SendMessageTargetResult
            {
                To = chatId,
                Success = false,
                Error = "chat_id empty"
            };
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new SendMessageTargetResult
            {
                To = chatId,
                Success = false,
                Error = "text empty"
            };
        }

        try
        {
            var baseUrl = (tg.ApiBaseUrl ?? "https://api.telegram.org").TrimEnd('/');
            var uri = $"{baseUrl}/bot{tg.BotToken.Trim()}/sendMessage";

            var payload = new TelegramSendMessageBody
            {
                ChatId = chatId.Trim(),
                Text = text,
                DisableWebPagePreview = disableWebPagePreview,
                ParseMode = string.IsNullOrWhiteSpace(parseMode) ? null : parseMode.Trim()
            };

            var client = _httpClientFactory.CreateClient("TelegramBot");
            using var response = await client.PostAsJsonAsync(uri, payload, JsonOptions, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Telegram sendMessage HTTP {Status} chat={Chat}: {Body}",
                    (int)response.StatusCode, chatId, body);
                return new SendMessageTargetResult
                {
                    To = chatId,
                    Success = false,
                    Error = $"HTTP {(int)response.StatusCode}: {Trim(body, 200)}"
                };
            }

            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var ok = doc.RootElement.TryGetProperty("ok", out var okProp) && okProp.ValueKind == JsonValueKind.True;
            if (!ok)
            {
                var desc = doc.RootElement.TryGetProperty("description", out var d) ? d.GetString() : body;
                return new SendMessageTargetResult
                {
                    To = chatId,
                    Success = false,
                    Error = desc ?? "Telegram ok=false"
                };
            }

            _logger.LogInformation("Telegram message sent to chat {ChatId}", chatId);
            return new SendMessageTargetResult { To = chatId, Success = true };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram sendMessage failed for chat {ChatId}", chatId);
            return new SendMessageTargetResult
            {
                To = chatId,
                Success = false,
                Error = ex.Message
            };
        }
    }

    private static string Trim(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Length <= max ? s : s[..max] + "…";
    }

    private sealed class TelegramSendMessageBody
    {
        [JsonPropertyName("chat_id")]
        public string ChatId { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("parse_mode")]
        public string? ParseMode { get; set; }

        [JsonPropertyName("disable_web_page_preview")]
        public bool DisableWebPagePreview { get; set; }
    }
}
