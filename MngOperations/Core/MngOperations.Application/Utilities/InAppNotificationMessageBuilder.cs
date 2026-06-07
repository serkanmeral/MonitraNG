using MngOperations.Application.Interfaces;

namespace MngOperations.Application.Utilities;

/// <summary>
/// In-app / toaster metinleri — state adları çözümlenmiş (ham ObjectId yok).
/// </summary>
public static class InAppNotificationMessageBuilder
{
    public static async Task<(string Title, string Message)> BuildAsync(
        NotificationDispatchRequest request,
        IMetadataCache metadataCache,
        string token,
        CancellationToken cancellationToken = default)
    {
        var workItemKey = request.WorkItemKey;
        var displayTitle = WorkItemDataHelper.GetString(request.WorkItem, "title");
        displayTitle = string.IsNullOrWhiteSpace(displayTitle) ? workItemKey : displayTitle.Trim();

        var fromState = await ResolveStateNameAsync(request.FromStateId, metadataCache, token, cancellationToken);
        var toState = await ResolveStateNameAsync(request.ToStateId, metadataCache, token, cancellationToken);
        var transitionLabel = FormatTransitionLabel(request.TransitionKey, fromState, toState);

        return request.EventType switch
        {
            "WorkItemCreated" => (
                $"Yeni iş: {workItemKey}",
                $"{workItemKey} oluşturuldu — {displayTitle}"),

            "WorkItemTransitioned" => (
                $"Durum değişti: {workItemKey}",
                $"{workItemKey}: {transitionLabel}"),

            "WorkItemUpdated" => (
                $"Güncellendi: {workItemKey}",
                $"{workItemKey} güncellendi — {displayTitle}"),

            _ => (
                $"Bildirim: {workItemKey}",
                $"{request.EventType} — {workItemKey}")
        };
    }

    private static string FormatTransitionLabel(string? transitionKey, string fromState, string toState)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(transitionKey))
            parts.Add(transitionKey.Trim());

        if (!string.IsNullOrWhiteSpace(fromState) || !string.IsNullOrWhiteSpace(toState))
        {
            var from = string.IsNullOrWhiteSpace(fromState) ? "…" : fromState;
            var to = string.IsNullOrWhiteSpace(toState) ? "…" : toState;
            parts.Add($"{from} → {to}");
        }

        return parts.Count > 0 ? string.Join(" · ", parts) : "durum güncellendi";
    }

    private static async Task<string> ResolveStateNameAsync(
        string? stateId,
        IMetadataCache metadataCache,
        string token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(stateId))
            return string.Empty;

        try
        {
            var state = await metadataCache.GetStateAsync(stateId, token, cancellationToken);
            return string.IsNullOrWhiteSpace(state.Name) ? stateId : state.Name.Trim();
        }
        catch
        {
            return stateId;
        }
    }
}
