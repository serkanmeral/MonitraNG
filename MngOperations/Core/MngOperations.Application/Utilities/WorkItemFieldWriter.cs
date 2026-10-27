using System.Text.Json;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Models;

namespace MngOperations.Application.Utilities;

public static class WorkItemFieldWriter
{
    public static void Apply(
        Dictionary<string, object?> target,
        IReadOnlyDictionary<string, object?> incoming,
        IReadOnlyDictionary<string, FieldRecord> enabledPoolFieldsByKey)
    {
        if (incoming.Count == 0)
            return;

        var extraFields = WorkItemDataHelper.CloneExtraFieldsDictionary(target);

        foreach (var (key, value) in incoming)
        {
            if (WorkItemCoreFields.IsReserved(key))
            {
                throw new OperationCoreException(
                    "RESERVED_FIELD",
                    $"Field '{key}' cannot be modified.",
                    $"'{key}' alanı değiştirilemez.",
                    400);
            }

            if (WorkItemCoreFields.IsWritable(key))
            {
                target[key] = string.Equals(key, "libraryDocumentIds", StringComparison.OrdinalIgnoreCase)
                    ? CoerceStringList(value)
                    : value;
                extraFields.Remove(key);
                continue;
            }

            if (enabledPoolFieldsByKey.ContainsKey(key))
            {
                if (IsEmptyValue(value))
                    extraFields.Remove(key);
                else
                    extraFields[key] = value;

                target.Remove(key);
                continue;
            }

            throw new OperationCoreException(
                "UNKNOWN_FIELD",
                $"Field '{key}' is not defined or not allowed.",
                $"'{key}' alanı tanımsız veya kullanılamaz.",
                400);
        }

        if (extraFields.Count == 0)
            target.Remove(WorkItemCoreFields.ExtraFieldsKey);
        else
            target[WorkItemCoreFields.ExtraFieldsKey] = extraFields;
    }

    private static List<string> CoerceStringList(object? value)
    {
        if (value is null) return [];
        if (value is JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.Array)
            {
                return el.EnumerateArray()
                    .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            if (el.ValueKind == JsonValueKind.String)
            {
                var single = el.GetString()?.Trim();
                return string.IsNullOrWhiteSpace(single) ? [] : [single];
            }
        }

        if (value is IEnumerable<object?> list && value is not string)
        {
            return list
                .Select(item => item?.ToString()?.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (value is string text && !string.IsNullOrWhiteSpace(text))
            return [text.Trim()];

        return [];
    }

    private static bool IsEmptyValue(object? value) =>
        value switch
        {
            null => true,
            string s => HtmlRichTextHelper.IsEffectivelyEmptyHtml(s),
            _ => false
        };
}
