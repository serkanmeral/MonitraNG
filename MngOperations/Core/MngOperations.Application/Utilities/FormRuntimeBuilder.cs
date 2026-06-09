using System.Text.Json;
using MngOperations.Application.Contracts.Runtime;

namespace MngOperations.Application.Utilities;

public static class FormRuntimeBuilder
{
    private static readonly string[] FallbackFieldKeys =
        ["title", "description", "typeId", "assignee", "priorityId", "boardId"];

    public static IReadOnlyDictionary<string, FormFieldRuntimeDto> BuildFields(
        string mode,
        IReadOnlyDictionary<string, object?>? workItemValues,
        JsonElement? defaultValues,
        JsonElement? layout,
        IReadOnlyDictionary<string, FormFieldDefinition> catalogByKey)
    {
        var orderedKeys = FormLayoutHelper.ExtractOrderedFieldKeys(layout);
        if (orderedKeys.Count == 0)
            orderedKeys = FallbackFieldKeys;

        var fields = new Dictionary<string, FormFieldRuntimeDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in orderedKeys)
        {
            if (!catalogByKey.TryGetValue(key, out var definition))
            {
                definition = new FormFieldDefinition(key, key, "text", IsPool: true);
            }

            object? value = null;
            if (string.Equals(mode, "edit", StringComparison.OrdinalIgnoreCase)
                && workItemValues != null)
            {
                value = WorkItemDataHelper.GetFieldValue(workItemValues, key);
            }
            else if (defaultValues is { ValueKind: JsonValueKind.Object }
                     && defaultValues.Value.TryGetProperty(key, out var defProp))
            {
                value = JsonSerializer.Deserialize<object?>(defProp.GetRawText());
            }

            fields[key] = new FormFieldRuntimeDto
            {
                Key = key,
                Label = definition.Label,
                FieldType = definition.FieldType,
                Value = NormalizeValue(value)
            };
        }

        return fields;
    }

    public static void ApplyDefaultValues(
        IDictionary<string, FormFieldRuntimeDto> fields,
        IReadOnlyDictionary<string, object?> defaultValues,
        bool overwriteExisting = false)
    {
        foreach (var (key, defaultValue) in defaultValues)
        {
            if (!fields.TryGetValue(key, out var field))
                continue;

            if (!overwriteExisting && field.Value != null && !IsEmptyValue(field.Value))
                continue;

            fields[key] = new FormFieldRuntimeDto
            {
                Key = field.Key,
                Label = field.Label,
                FieldType = field.FieldType,
                Value = NormalizeValue(defaultValue)
            };
        }
    }

    private static bool IsEmptyValue(object? value) =>
        value switch
        {
            null => true,
            string s => HtmlRichTextHelper.IsEffectivelyEmptyHtml(s),
            JsonElement el when el.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined => true,
            JsonElement el when el.ValueKind == JsonValueKind.String =>
                HtmlRichTextHelper.IsEffectivelyEmptyHtml(el.GetString()),
            JsonElement el when el.ValueKind == JsonValueKind.Array => !el.EnumerateArray().Any(),
            System.Collections.IEnumerable e when value is not string => !e.GetEnumerator().MoveNext(),
            _ => false
        };

    public static IReadOnlyDictionary<string, FieldBehaviorDto> ParseFieldBehaviors(JsonElement? behaviors, string mode)
    {
        var result = new Dictionary<string, FieldBehaviorDto>(StringComparer.OrdinalIgnoreCase);
        var isEdit = string.Equals(mode, "edit", StringComparison.OrdinalIgnoreCase);

        foreach (var key in FallbackFieldKeys)
        {
            result[key] = new FieldBehaviorDto
            {
                Visible = true,
                Readonly = isEdit && key is "typeId",
                Required = key is "title" or "typeId"
            };
        }

        if (behaviors is not { ValueKind: JsonValueKind.Object })
            return result;

        foreach (var prop in behaviors.Value.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.Object)
                continue;

            var visible = ReadBool(prop.Value, "visible", true);
            var readOnly = ReadBool(prop.Value, "readonly", false) || ReadBool(prop.Value, "readOnly", false);
            var required = ReadBool(prop.Value, "required", false);
            var masked = ReadBool(prop.Value, "masked", false);

            result[prop.Name] = new FieldBehaviorDto
            {
                Visible = visible,
                Readonly = readOnly,
                Required = required,
                Masked = masked
            };
        }

        return result;
    }

    private static bool ReadBool(JsonElement element, string name, bool defaultValue)
    {
        if (!element.TryGetProperty(name, out var prop))
            return defaultValue;

        return prop.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => defaultValue
        };
    }

    private static object? NormalizeValue(object? value)
    {
        if (value is JsonElement el)
        {
            return el.ValueKind switch
            {
                JsonValueKind.String => el.GetString(),
                JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => JsonSerializer.Deserialize<object?>(el.GetRawText())
            };
        }

        return value;
    }
}
