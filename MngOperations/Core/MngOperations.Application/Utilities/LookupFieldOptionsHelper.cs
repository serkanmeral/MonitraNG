using System.Text.Json;

namespace MngOperations.Application.Utilities;

/// <summary>
/// op_fields.options.lookup — profil/aktivite read-time relation etiket çözümü.
/// UI: Mng.Ui/utils/ocLookupFieldOptions.ts ile hizalı.
/// </summary>
public static class LookupFieldOptionsHelper
{
    public const string DefaultLabelField = "name";
    public const string DefaultValueField = "__dataId";

    public static string ResolveLabelField(IReadOnlyDictionary<string, object?>? poolField)
    {
        if (poolField == null)
            return DefaultLabelField;

        var fromLookup = GetNestedString(poolField, "options", "lookup", "labelField")
            ?? GetNestedString(poolField, "options", "lookup", "displayField");
        if (!string.IsNullOrWhiteSpace(fromLookup))
            return fromLookup.Trim();

        var legacy = GetNestedString(poolField, "options", "labelField");
        if (!string.IsNullOrWhiteSpace(legacy))
            return legacy.Trim();

        return DefaultLabelField;
    }

    /// <summary>Dataset satırından görünen metin — önce metadata labelField, sonra bilinen yedekler.</summary>
    public static string? ResolveDisplayText(IReadOnlyDictionary<string, object?> row, string labelField)
    {
        var primary = WorkItemDataHelper.GetString(row, labelField);
        if (!string.IsNullOrWhiteSpace(primary))
            return primary.Trim();

        foreach (var fallback in new[] { "name", "label", "title", "unvan", "key" })
        {
            if (string.Equals(fallback, labelField, StringComparison.OrdinalIgnoreCase))
                continue;
            var v = WorkItemDataHelper.GetString(row, fallback);
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }

        var id = WorkItemDataHelper.GetString(row, "__dataId");
        return string.IsNullOrWhiteSpace(id) ? null : id;
    }

    public static string ComposeResolveKey(string dataset, string labelField) =>
        $"{dataset}\u001f{labelField}";

    public static (string Dataset, string LabelField) ParseResolveKey(string key)
    {
        var idx = key.IndexOf('\u001f');
        if (idx < 0)
            return (key, DefaultLabelField);
        return (key[..idx], key[(idx + 1)..]);
    }

    private static string? GetNestedString(
        IReadOnlyDictionary<string, object?> data,
        params string[] path)
    {
        object? current = data;
        foreach (var segment in path)
        {
            if (current == null)
                return null;

            if (current is IReadOnlyDictionary<string, object?> dict)
            {
                if (!dict.TryGetValue(segment, out current) || current == null)
                    return null;
                continue;
            }

            if (current is JsonElement el)
            {
                if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(segment, out var next))
                    return null;
                current = next;
                continue;
            }

            if (current is Dictionary<string, object?> mutDict)
            {
                if (!mutDict.TryGetValue(segment, out current) || current == null)
                    return null;
                continue;
            }

            return null;
        }

        return current switch
        {
            null => null,
            string s => s,
            JsonElement el when el.ValueKind == JsonValueKind.String => el.GetString(),
            _ => current.ToString()
        };
    }
}
