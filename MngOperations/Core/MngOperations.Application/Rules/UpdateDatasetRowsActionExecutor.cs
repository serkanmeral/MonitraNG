using System.Text.Json;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Utilities;

namespace MngOperations.Application.Rules;

public interface IUpdateDatasetRowsActionExecutor
{
    Task<UpdateDatasetRowsResult> ExecuteAsync(
        IReadOnlyDictionary<string, object?> payload,
        IReadOnlyDictionary<string, object?> workItem,
        string workItemId,
        string workItemKey,
        string token,
        CancellationToken cancellationToken = default);
}

public sealed class UpdateDatasetRowsResult
{
    public int UpdatedCount { get; init; }
    public IReadOnlyList<string> UpdatedIds { get; init; } = Array.Empty<string>();
    public string? Dataset { get; init; }
}

/// <summary>
/// Plans <c>updateDatasetRows</c> patches. Reuses createDatasetRows cardinality + fieldMappings;
/// each row also resolves <c>targetId</c> (dataset row id to PUT).
/// </summary>
public static class UpdateDatasetRowsPlanner
{
    public const int DefaultMaxRows = CreateDatasetRowsPlanner.DefaultMaxRows;

    public static JsonElement ResolveActionElement(IReadOnlyDictionary<string, object?> payload) =>
        CreateDatasetRowsPlanner.ResolveActionElement(payload);

    public static string? GetString(JsonElement element, string name) =>
        CreateDatasetRowsPlanner.GetString(element, name);

    /// <summary>Returns (targetRowId, patchFields) pairs in cardinality order.</summary>
    public static List<(string TargetId, Dictionary<string, object?> Patch)> BuildUpdates(
        JsonElement action,
        IReadOnlyDictionary<string, object?> workItem,
        string workItemId,
        string workItemKey)
    {
        var patches = CreateDatasetRowsPlanner.BuildRows(action, workItem, workItemId, workItemKey);
        var targetIds = ResolveTargetIds(action, workItem, workItemId, workItemKey, patches.Count);

        if (targetIds.Count != patches.Count)
        {
            throw new OperationCoreException(
                "UPDATE_DATASET_ROWS_TARGET_MISMATCH",
                $"targetId count ({targetIds.Count}) does not match patch count ({patches.Count}).",
                $"Hedef id sayısı ({targetIds.Count}) güncelleme satırı ({patches.Count}) ile uyuşmuyor.",
                400);
        }

        var result = new List<(string, Dictionary<string, object?>)>(patches.Count);
        var clearFields = ParseClearFields(action);
        for (var i = 0; i < patches.Count; i++)
        {
            var id = targetIds[i];
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new OperationCoreException(
                    "UPDATE_DATASET_ROWS_TARGET_ID",
                    $"targetId is empty at index {i}.",
                    $"Hedef satır id boş (index {i}).",
                    400);
            }

            var patch = patches[i];
            foreach (var field in clearFields)
            {
                if (!string.IsNullOrWhiteSpace(field))
                    patch[field] = null;
            }

            result.Add((id.Trim(), patch));
        }

        return result;
    }

    private static IReadOnlyList<string> ParseClearFields(JsonElement action)
    {
        if (!action.TryGetProperty("clearFields", out var raw) || raw.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        var list = new List<string>();
        foreach (var el in raw.EnumerateArray())
        {
            var s = el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
            if (!string.IsNullOrWhiteSpace(s))
                list.Add(s.Trim());
        }

        return list;
    }

    private static List<string> ResolveTargetIds(
        JsonElement action,
        IReadOnlyDictionary<string, object?> workItem,
        string workItemId,
        string workItemKey,
        int expectedCount)
    {
        if (!action.TryGetProperty("targetId", out var targetIdEl)
            || targetIdEl.ValueKind != JsonValueKind.Object)
        {
            throw new OperationCoreException(
                "UPDATE_DATASET_ROWS_TARGET_ID",
                "updateDatasetRows.targetId is required.",
                "targetId zorunludur.",
                400);
        }

        var cardinality = action.TryGetProperty("cardinality", out var card) && card.ValueKind == JsonValueKind.Object
            ? card
            : default;
        var mode = GetString(cardinality, "mode") ?? "single";
        var itemAs = GetString(cardinality, "itemAs") ?? "item";

        if (string.Equals(mode, "single", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mode, "count", StringComparison.OrdinalIgnoreCase))
        {
            var ids = new List<string>(expectedCount);
            for (var i = 0; i < expectedCount; i++)
            {
                var id = ResolveOneTargetId(targetIdEl, workItem, workItemId, workItemKey, itemContext: null);
                ids.Add(id ?? string.Empty);
            }

            return ids;
        }

        if (string.Equals(mode, "expand", StringComparison.OrdinalIgnoreCase))
        {
            var itemsPath = GetString(cardinality, "itemsFrom");
            var itemsRaw = WorkItemPathValueResolver.Resolve(itemsPath, workItem, workItemId, workItemKey);
            var items = WorkItemPathValueResolver.ToList(itemsRaw);
            var ids = new List<string>(items.Count);
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var ctx = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    [itemAs] = item,
                    ["value"] = item
                };
                var id = ResolveOneTargetId(targetIdEl, workItem, workItemId, workItemKey, ctx);
                ids.Add(id ?? string.Empty);
            }

            return ids;
        }

        throw new OperationCoreException(
            "UPDATE_DATASET_ROWS_CARDINALITY",
            $"Unknown cardinality.mode '{mode}'.",
            $"Bilinmeyen cardinality.mode: {mode}",
            400);
    }

    private static string? ResolveOneTargetId(
        JsonElement targetIdMapping,
        IReadOnlyDictionary<string, object?> workItem,
        string workItemId,
        string workItemKey,
        IReadOnlyDictionary<string, object?>? itemContext)
    {
        var source = (GetString(targetIdMapping, "source") ?? "item").ToLowerInvariant();
        object? value = source switch
        {
            "static" => GetRawValue(targetIdMapping, "value"),
            "item" => ResolveItemValue(targetIdMapping, itemContext),
            "field" => WorkItemPathValueResolver.Resolve(
                GetString(targetIdMapping, "path"),
                workItem,
                workItemId,
                workItemKey,
                itemContext),
            "token" => WorkItemPathValueResolver.ResolveTemplate(
                GetString(targetIdMapping, "template") ?? GetString(targetIdMapping, "value"),
                workItem,
                workItemId,
                workItemKey,
                itemContext),
            _ => null
        };

        return WorkItemPathValueResolver.FormatScalar(value);
    }

    private static object? ResolveItemValue(
        JsonElement mapping,
        IReadOnlyDictionary<string, object?>? itemContext)
    {
        if (itemContext is null)
            return null;

        var path = GetString(mapping, "path") ?? "value";
        if (itemContext.TryGetValue(path, out var direct))
            return direct;

        if (path.Contains('.', StringComparison.Ordinal))
        {
            var leaf = path.Split('.').Last();
            if (itemContext.TryGetValue(leaf, out var leafVal))
                return leafVal;
        }

        return itemContext.TryGetValue("value", out var fallback) ? fallback : null;
    }

    private static object? GetRawValue(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var prop))
            return null;

        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString(),
            JsonValueKind.Number => prop.TryGetInt64(out var l) ? l : prop.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => JsonSerializer.Deserialize<object?>(prop.GetRawText())
        };
    }
}
