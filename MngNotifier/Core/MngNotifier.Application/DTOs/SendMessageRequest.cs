using System.Text.Json;

namespace MngNotifier.Application.DTOs;

/// <summary>
/// Push-only messaging (Telegram MVP). Not a chatbot — one-way notify.
/// Provide <see cref="Text"/> or <see cref="TemplateKey"/> (+ context).
/// </summary>
public class SendMessageRequest
{
    /// <summary>Channel id: <c>telegram</c> (MVP). Future: whatsapp, slack.</summary>
    public string Channel { get; set; } = "telegram";

    /// <summary>
    /// Channel addresses. For Telegram: numeric chat_id strings.
    /// Empty → uses configured DefaultChatId when present.
    /// </summary>
    public List<string> To { get; set; } = new();

    /// <summary>Plain or HTML body (Telegram parse_mode). Optional when TemplateKey is set.</summary>
    public string? Text { get; set; }

    /// <summary>Optional @message_templates key; renders bodyText with Context.</summary>
    public string? TemplateKey { get; set; }

    /// <summary>Context for TemplateKey placeholders ({{path}}).</summary>
    public JsonElement Context { get; set; }

    /// <summary>Optional Telegram parse mode: HTML | MarkdownV2 | empty. Template parseMode used when null.</summary>
    public string? ParseMode { get; set; }

    /// <summary>Optional disable web page preview.</summary>
    public bool DisableWebPagePreview { get; set; } = true;
}

public class SendMessageResponse
{
    public string NotificationId { get; set; } = string.Empty;
    public string Status { get; set; } = "sent";
    public string Channel { get; set; } = string.Empty;
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
    public List<SendMessageTargetResult> Results { get; set; } = new();
    public DateTime QueuedAt { get; set; }
}

public class SendMessageTargetResult
{
    public string To { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
}
