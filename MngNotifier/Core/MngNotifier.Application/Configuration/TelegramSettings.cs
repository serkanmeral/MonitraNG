namespace MngNotifier.Application.Configuration;

/// <summary>Telegram Bot API — push-only notifications (not a chatbot).</summary>
public class TelegramSettings
{
    /// <summary>Master switch. When false, send-message / polling / webhook bind are off.</summary>
    public bool Enabled { get; set; }

    /// <summary>BotFather token. Prefer env: MngNotifierSettings__Telegram__BotToken</summary>
    public string BotToken { get; set; } = string.Empty;

    /// <summary>Public bot username without @ (deep links). Example: MonitraNGBot</summary>
    public string BotUsername { get; set; } = "MonitraNGBot";

    /// <summary>Optional default chat/group id when request.To is empty.</summary>
    public string? DefaultChatId { get; set; }

    /// <summary>Telegram Bot API root (no trailing slash).</summary>
    public string ApiBaseUrl { get; set; } = "https://api.telegram.org";

    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Local/dev: long-poll getUpdates. Disable when using a public WebhookPublicUrl.
    /// </summary>
    public bool UsePolling { get; set; } = true;

    /// <summary>
    /// Public HTTPS URL for Telegram setWebhook (e.g. https://notify.example.com/api/v1/telegram/webhook).
    /// When set, polling should be false.
    /// </summary>
    public string? WebhookPublicUrl { get; set; }

    /// <summary>Optional secret_token for webhook header X-Telegram-Bot-Api-Secret-Token.</summary>
    public string? WebhookSecretToken { get; set; }

    /// <summary>MngKeeper base URL for internal telegram-link.</summary>
    public string KeeperBaseUrl { get; set; } = "http://mngkeeper:5001";

    /// <summary>Start payload prefix: link_{domainId}_{userId}</summary>
    public string LinkPayloadPrefix { get; set; } = "link_";

    /// <summary>getUpdates long-poll timeout seconds.</summary>
    public int PollingTimeoutSeconds { get; set; } = 25;
}
