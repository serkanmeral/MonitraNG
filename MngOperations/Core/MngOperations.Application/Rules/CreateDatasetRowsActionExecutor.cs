using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Utilities;

namespace MngOperations.Application.Rules;

public interface ICreateDatasetRowsActionExecutor
{
    Task<CreateDatasetRowsResult> ExecuteAsync(
        IReadOnlyDictionary<string, object?> payload,
        IReadOnlyDictionary<string, object?> workItem,
        string workItemId,
        string workItemKey,
        string token,
        CancellationToken cancellationToken = default);
}

public sealed class CreateDatasetRowsResult
{
    public bool SkippedIdempotent { get; init; }
    public int CreatedCount { get; init; }
    public IReadOnlyList<string> CreatedIds { get; init; } = Array.Empty<string>();
    public string? Dataset { get; init; }
}

/// <summary>Pure row planning for <c>createDatasetRows</c> (testable without DG).</summary>
public static partial class CreateDatasetRowsPlanner
{
    public const int DefaultMaxRows = 500;

    private static readonly Regex SequencePadRegex = SequencePadPattern();

    public static JsonElement ResolveActionElement(IReadOnlyDictionary<string, object?> payload)
    {
        if (payload.TryGetValue("actionJson", out var raw) && raw is string json && !string.IsNullOrWhiteSpace(json))
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }

        return JsonSerializer.SerializeToElement(payload);
    }

    public static List<Dictionary<string, object?>> BuildRows(
        JsonElement action,
        IReadOnlyDictionary<string, object?> workItem,
        string workItemId,
        string workItemKey)
    {
        var mappings = ParseMappings(action);
        var cardinality = action.TryGetProperty("cardinality", out var card) && card.ValueKind == JsonValueKind.Object
            ? card
            : default;

        var mode = GetString(cardinality, "mode") ?? "single";
        var itemAs = GetString(cardinality, "itemAs") ?? "item";

        if (string.Equals(mode, "single", StringComparison.OrdinalIgnoreCase))
        {
            return new List<Dictionary<string, object?>>
            {
                BuildRow(mappings, workItem, workItemId, workItemKey, itemContext: null, rowIndex: 0)
            };
        }

        if (string.Equals(mode, "count", StringComparison.OrdinalIgnoreCase))
        {
            var countPath = GetString(cardinality, "countFrom");
            var countVal = WorkItemPathValueResolver.Resolve(countPath, workItem, workItemId, workItemKey);
            var n = WorkItemPathValueResolver.ToInt(countVal) ?? 0;
            if (n <= 0)
            {
                throw Fail(
                    "CREATE_DATASET_ROWS_COUNT",
                    $"Invalid countFrom '{countPath}' value.",
                    $"Geçersiz miktar alanı: {countPath}");
            }

            var rows = new List<Dictionary<string, object?>>(n);
            for (var i = 0; i < n; i++)
                rows.Add(BuildRow(mappings, workItem, workItemId, workItemKey, itemContext: null, rowIndex: i));
            return rows;
        }

        if (string.Equals(mode, "expand", StringComparison.OrdinalIgnoreCase))
        {
            var itemsPath = GetString(cardinality, "itemsFrom");
            var itemsRaw = WorkItemPathValueResolver.Resolve(itemsPath, workItem, workItemId, workItemKey);
            var items = WorkItemPathValueResolver.ToList(itemsRaw);

            var countPath = GetString(cardinality, "countFrom");
            if (!string.IsNullOrWhiteSpace(countPath))
            {
                var countVal = WorkItemPathValueResolver.Resolve(countPath, workItem, workItemId, workItemKey);
                var expected = WorkItemPathValueResolver.ToInt(countVal);
                if (expected is null || expected != items.Count)
                {
                    throw Fail(
                        "CREATE_DATASET_ROWS_EXPAND_MISMATCH",
                        $"itemsFrom length ({items.Count}) does not match countFrom ({expected}).",
                        $"Liste uzunluğu ({items.Count}) miktar ({expected}) ile uyuşmuyor.");
                }
            }

            if (items.Count == 0)
            {
                throw Fail(
                    "CREATE_DATASET_ROWS_EXPAND_EMPTY",
                    $"itemsFrom '{itemsPath}' is empty.",
                    $"Genişletme listesi boş: {itemsPath}");
            }

            var rows = new List<Dictionary<string, object?>>(items.Count);
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var ctx = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    [itemAs] = item,
                    ["value"] = item
                };
                rows.Add(BuildRow(mappings, workItem, workItemId, workItemKey, ctx, rowIndex: i));
            }

            return rows;
        }

        throw Fail(
            "CREATE_DATASET_ROWS_CARDINALITY",
            $"Unknown cardinality.mode '{mode}'.",
            $"Bilinmeyen cardinality.mode: {mode}");
    }

    public static string? GetString(JsonElement element, string name)
    {
        if (element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return null;
        if (!element.TryGetProperty(name, out var prop))
            return null;
        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
    }

    private static Dictionary<string, object?> BuildRow(
        IReadOnlyList<JsonElement> mappings,
        IReadOnlyDictionary<string, object?> workItem,
        string workItemId,
        string workItemKey,
        IReadOnlyDictionary<string, object?>? itemContext,
        int rowIndex)
    {
        var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var mapping in mappings)
        {
            var target = GetString(mapping, "target");
            if (string.IsNullOrWhiteSpace(target))
                continue;

            var source = (GetString(mapping, "source") ?? "field").ToLowerInvariant();
            object? value = source switch
            {
                "static" => GetRawValue(mapping, "value"),
                "token" => WorkItemPathValueResolver.ResolveTemplate(
                    GetString(mapping, "template") ?? GetString(mapping, "value"),
                    workItem,
                    workItemId,
                    workItemKey,
                    itemContext),
                "item" => ResolveItemMapping(mapping, itemContext),
                "sequence" => ResolveSequenceMapping(mapping, workItem, workItemId, workItemKey, itemContext, rowIndex),
                "field" => WorkItemPathValueResolver.Resolve(
                    GetString(mapping, "path"),
                    workItem,
                    workItemId,
                    workItemKey,
                    itemContext),
                _ => null
            };

            if (value is not null)
                row[target] = value;
        }

        return row;
    }

    /// <summary>
    /// Builds incremental values: template may include <c>{{source.key}}</c> tokens and
    /// zero-padded index placeholders such as <c>{0}</c>, <c>{00}</c>, <c>{000}</c>.
    /// Sequence number = startFrom + rowIndex (0-based).
    /// </summary>
    private static object? ResolveSequenceMapping(
        JsonElement mapping,
        IReadOnlyDictionary<string, object?> workItem,
        string workItemId,
        string workItemKey,
        IReadOnlyDictionary<string, object?>? itemContext,
        int rowIndex)
    {
        var template = GetString(mapping, "template") ?? GetString(mapping, "value");
        if (string.IsNullOrWhiteSpace(template))
        {
            throw Fail(
                "CREATE_DATASET_ROWS_SEQUENCE",
                "sequence mapping requires template (e.g. SERI-{000}).",
                "sequence eşlemesi için şablon gerekli (örn. SERI-{000}).");
        }

        var startFrom = 1;
        var startFromPath = GetString(mapping, "startFromPath");
        if (!string.IsNullOrWhiteSpace(startFromPath))
        {
            var raw = WorkItemPathValueResolver.Resolve(startFromPath, workItem, workItemId, workItemKey, itemContext);
            var parsed = WorkItemPathValueResolver.ToInt(raw);
            if (parsed is null)
            {
                throw Fail(
                    "CREATE_DATASET_ROWS_SEQUENCE",
                    $"Invalid startFromPath '{startFromPath}' value.",
                    $"Geçersiz başlangıç alanı: {startFromPath}");
            }

            startFrom = parsed.Value;
        }
        else
        {
            var startRaw = GetRawValue(mapping, "startFrom");
            var parsed = WorkItemPathValueResolver.ToInt(startRaw);
            if (parsed is not null)
                startFrom = parsed.Value;
        }

        if (startFrom < 0)
        {
            throw Fail(
                "CREATE_DATASET_ROWS_SEQUENCE",
                "startFrom must be >= 0.",
                "startFrom 0 veya daha büyük olmalı.");
        }

        var sequenceValue = startFrom + rowIndex;
        var withTokens = WorkItemPathValueResolver.ResolveTemplate(
            template,
            workItem,
            workItemId,
            workItemKey,
            itemContext);

        if (!SequencePadRegex.IsMatch(withTokens))
            return withTokens + sequenceValue.ToString(CultureInfo.InvariantCulture);

        return SequencePadRegex.Replace(withTokens, match =>
        {
            var width = match.Groups[1].Value.Length;
            return sequenceValue.ToString($"D{width}", CultureInfo.InvariantCulture);
        });
    }

    private static object? ResolveItemMapping(
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

    private static IReadOnlyList<JsonElement> ParseMappings(JsonElement action)
    {
        if (!action.TryGetProperty("fieldMappings", out var mappings)
            || mappings.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<JsonElement>();
        }

        return mappings.EnumerateArray().ToList();
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

    private static OperationCoreException Fail(string code, string en, string tr) =>
        new(code, en, tr, 400);

    [GeneratedRegex(@"\{(0+)\}", RegexOptions.Compiled)]
    private static partial Regex SequencePadPattern();
}
