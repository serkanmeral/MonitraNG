namespace MngOperations.Application.Utilities;

public static class NotificationMessageBuilder
{
    public static (string Title, string Message, string Subject, string HtmlBody) Build(
        string eventType,
        string workItemKey,
        string? title,
        string? transitionKey = null,
        string? fromStateId = null,
        string? toStateId = null)
    {
        var displayTitle = string.IsNullOrWhiteSpace(title) ? workItemKey : title.Trim();

        return eventType switch
        {
            "WorkItemCreated" => (
                $"Yeni iş: {workItemKey}",
                $"{workItemKey} oluşturuldu — {displayTitle}",
                $"[Operation Core] {workItemKey} oluşturuldu",
                $"<p><strong>{workItemKey}</strong> oluşturuldu.</p><p>{Escape(displayTitle)}</p>"),

            "WorkItemTransitioned" => (
                $"Durum değişti: {workItemKey}",
                $"{workItemKey}: {transitionKey ?? "transition"} ({fromStateId} → {toStateId})",
                $"[Operation Core] {workItemKey} durum değişikliği",
                $"<p><strong>{workItemKey}</strong> — transition <code>{Escape(transitionKey)}</code></p>" +
                $"<p>{Escape(fromStateId)} → {Escape(toStateId)}</p><p>{Escape(displayTitle)}</p>"),

            "WorkItemUpdated" => (
                $"Güncellendi: {workItemKey}",
                $"{workItemKey} güncellendi — {displayTitle}",
                $"[Operation Core] {workItemKey} güncellendi",
                $"<p><strong>{workItemKey}</strong> güncellendi.</p><p>{Escape(displayTitle)}</p>"),

            _ => (
                $"Operation Core: {workItemKey}",
                $"{eventType} — {workItemKey}",
                $"[Operation Core] {workItemKey}",
                $"<p>{Escape(eventType)} — <strong>{workItemKey}</strong></p>")
        };
    }

    private static string Escape(string? value) =>
        string.IsNullOrEmpty(value)
            ? string.Empty
            : System.Net.WebUtility.HtmlEncode(value);
}
