namespace MngDocument.Application.Configuration;

/// <summary>D-N — document.generated notifications (mail + Telegram via MngNotifier).</summary>
public sealed class DocumentNotificationsSettings
{
    /// <summary>Master switch. When false, orchestrator is a no-op.</summary>
    public bool Enabled { get; set; }

    /// <summary>MngNotifier base URL (e.g. http://mngnotifier:5070).</summary>
    public string NotifierBaseUrl { get; set; } = "http://mngnotifier:5070";

    public string NotifierApiVersion { get; set; } = "v1";

    /// <summary>
    /// Channels to use: <c>email</c>, <c>telegram</c>.
    /// Empty → treated as email only (backward compatible).
    /// </summary>
    public List<string> Channels { get; set; } = new() { "email" };

    /// <summary>Default mail recipients when request/profile does not supply any.</summary>
    public List<string> DefaultTo { get; set; } = new();

    /// <summary>Static Telegram chat_ids (group/ops or known DMs).</summary>
    public List<string> DefaultTelegramChatIds { get; set; } = new();

    /// <summary>
    /// Keeper user ids whose <c>telegramChatId</c> will be resolved (requires DomainId).
    /// </summary>
    public List<string> TelegramUserIds { get; set; } = new();

    /// <summary>Domain id for Keeper telegram-resolve.</summary>
    public string? DomainId { get; set; }

    /// <summary>MngKeeper base URL for recipient resolve.</summary>
    public string KeeperBaseUrl { get; set; } = "http://mngkeeper:5001";

    /// <summary>Shared with Notifier/Keeper internal endpoints when configured.</summary>
    public string? InternalNotifyApiKey { get; set; }

    /// <summary>UI origin for deep links (no trailing slash), e.g. http://localhost:3000.</summary>
    public string UiBaseUrl { get; set; } = "http://localhost:3000";

    /// <summary>Path template; {id} replaced with resource id.</summary>
    public string DeepLinkPathTemplate { get; set; } = "/apps/document-intelligence/r/{id}";
}
