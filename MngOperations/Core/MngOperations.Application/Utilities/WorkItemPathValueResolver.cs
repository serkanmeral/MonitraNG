using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MngOperations.Application.Utilities;

/// <summary>
/// Resolves WI paths (<c>key</c>, <c>id</c>, <c>fields.x</c>) to typed values for rule mappings.
/// </summary>
public static partial class WorkItemPathValueResolver
{
    private static readonly Regex TokenRegex = TokenPattern();

    public static object? Resolve(
        string? path,
        IReadOnlyDictionary<string, object?> workItem,
        string? workItemId = null,
        string? workItemKey = null,
        IReadOnlyDictionary<string, object?>? itemContext = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var normalized = path.Trim();

        if (normalized.StartsWith("source.", StringComparison.OrdinalIgnoreCase))
            normalized = normalized["source.".Length..];

        if (string.Equals(normalized, "id", StringComparison.OrdinalIgnoreCase))
            return workItemId ?? WorkItemDataHelper.GetDataId(workItem);

        if (string.Equals(normalized, "key", StringComparison.OrdinalIgnoreCase))
            return workItemKey ?? WorkItemDataHelper.GetString(workItem, "key");

        if (itemContext != null
            && normalized.StartsWith("item.", StringComparison.OrdinalIgnoreCase))
        {
            var itemKey = normalized["item.".Length..];
            return itemContext.TryGetValue(itemKey, out var itemVal) ? itemVal : null;
        }

        if (itemContext != null
            && !normalized.Contains('.', StringComparison.Ordinal)
            && itemContext.TryGetValue(normalized, out var directItem))
        {
            return directItem;
        }

        if (normalized.StartsWith("fields.", StringComparison.OrdinalIgnoreCase))
        {
            var fieldKey = normalized["fields.".Length..];
            return WorkItemDataHelper.GetFieldValue(workItem, fieldKey);
        }

        return WorkItemDataHelper.GetFieldValue(workItem, normalized)
            ?? WorkItemDataHelper.GetString(workItem, normalized);
    }

    public static string ResolveTemplate(
        string? template,
        IReadOnlyDictionary<string, object?> workItem,
        string? workItemId = null,
        string? workItemKey = null,
        IReadOnlyDictionary<string, object?>? itemContext = null)
    {
        if (string.IsNullOrWhiteSpace(template))
            return string.Empty;

        return TokenRegex.Replace(template, match =>
        {
            var path = match.Groups[1].Value.Trim();
            if (path.StartsWith("source.", StringComparison.OrdinalIgnoreCase))
                path = path["source.".Length..];

            var value = Resolve(path, workItem, workItemId, workItemKey, itemContext);
            return FormatScalar(value) ?? string.Empty;
        });
    }

    public static string? FormatScalar(object? value)
    {
        if (value is null)
            return null;

        return value switch
        {
            string s => s,
            bool b => b ? "true" : "false",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            JsonElement el => el.ValueKind switch
            {
                JsonValueKind.String => el.GetString(),
                JsonValueKind.Number => el.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => null,
                _ => el.ToString()
            },
            _ => value.ToString()
        };
    }

    public static IReadOnlyList<object?> ToList(object? value)
    {
        if (value is null)
            return Array.Empty<object?>();

        if (value is JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.Array)
            {
                var flattened = new List<object?>();
                foreach (var item in el.EnumerateArray())
                    flattened.AddRange(ToList(DeserializeElement(item)));
                return flattened;
            }

            if (el.ValueKind == JsonValueKind.String)
                return SplitDelimitedText(el.GetString());

            return new object?[] { DeserializeElement(el) };
        }

        if (value is string text)
            return SplitDelimitedText(text);

        if (value is IEnumerable<object?> enumerable && value is not string)
        {
            var flattened = new List<object?>();
            foreach (var item in enumerable)
                flattened.AddRange(ToList(item));
            return flattened;
        }

        if (value is System.Collections.IEnumerable raw && value is not string)
        {
            var flattened = new List<object?>();
            foreach (var item in raw)
                flattened.AddRange(ToList(item));
            return flattened;
        }

        return new object?[] { value };
    }

    /// <summary>
    /// Splits text lists used in OC pool fields (e.g. seriNoListesi):
    /// <c>A;B;C</c>, <c>A,B,C</c>, newlines, or whitespace-separated tokens.
    /// </summary>
    public static IReadOnlyList<object?> SplitDelimitedText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<object?>();

        var trimmed = text.Trim();
        char[] delimiters = [';', ',', '\n', '\r', '|'];
        if (trimmed.IndexOfAny(delimiters) >= 0)
        {
            return trimmed
                .Split(delimiters, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Cast<object?>()
                .ToList();
        }

        // "SN1 SN2 SN3" — only when multiple whitespace tokens (single serial with spaces stays intact)
        var whitespaceParts = trimmed.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (whitespaceParts.Length > 1)
            return whitespaceParts.Cast<object?>().ToList();

        return new object?[] { trimmed };
    }

    public static int? ToInt(object? value)
    {
        if (value is null)
            return null;

        return value switch
        {
            int i => i,
            long l => (int)l,
            double d => (int)d,
            decimal m => (int)m,
            float f => (int)f,
            string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            JsonElement el when el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n) => n,
            JsonElement el when el.ValueKind == JsonValueKind.String
                && int.TryParse(el.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ns) => ns,
            _ => int.TryParse(FormatScalar(value), NumberStyles.Integer, CultureInfo.InvariantCulture, out var fallback)
                ? fallback
                : null
        };
    }

    private static object? DeserializeElement(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => JsonSerializer.Deserialize<object?>(element.GetRawText())
        };

    [GeneratedRegex(@"\{\{([^}]+)\}\}", RegexOptions.Compiled)]
    private static partial Regex TokenPattern();
}
