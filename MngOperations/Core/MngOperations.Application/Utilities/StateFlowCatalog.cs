using System.Text.Json;
using MngOperations.Application.Exceptions;

namespace MngOperations.Application.Utilities;

public static class StateFlowCatalog
{
    public static JsonElement? FindTransition(JsonElement? transitions, string transitionKey, string fromStateId)
    {
        if (transitions is not { ValueKind: JsonValueKind.Array })
            return null;

        foreach (var item in transitions.Value.EnumerateArray())
        {
            var key = GetString(item, "transitionKey");
            var from = GetString(item, "fromStateId");

            if (string.Equals(key, transitionKey, StringComparison.OrdinalIgnoreCase)
                && string.Equals(from, fromStateId, StringComparison.Ordinal))
            {
                return item;
            }
        }

        return null;
    }

    public static IReadOnlyList<JsonElement> ListFromState(JsonElement? transitions, string fromStateId)
    {
        if (transitions is not { ValueKind: JsonValueKind.Array })
            return Array.Empty<JsonElement>();

        return transitions.Value.EnumerateArray()
            .Where(t => string.Equals(GetString(t, "fromStateId"), fromStateId, StringComparison.Ordinal))
            .ToList();
    }

    public static string? GetToStateId(JsonElement transition) => GetString(transition, "toStateId");

    public static JsonElement? GetPermissions(JsonElement transition)
    {
        if (transition.TryGetProperty("permissions", out var permissions)
            && permissions.ValueKind == JsonValueKind.Object)
        {
            return permissions;
        }

        return null;
    }

    public static IReadOnlyList<string> GetPermissionGroups(JsonElement? permissions)
    {
        if (permissions is not { ValueKind: JsonValueKind.Object })
            return Array.Empty<string>();

        if (!permissions.Value.TryGetProperty("groups", out var groups))
            return Array.Empty<string>();

        return GroupListParser.Parse(groups);
    }

    public static void EnsureTransitionValid(JsonElement transition, string transitionKey, string workItemStateId)
    {
        var from = GetString(transition, "fromStateId");
        if (!string.Equals(from, workItemStateId, StringComparison.Ordinal))
        {
            throw new OperationCoreException(
                "TRANSITION_INVALID_STATE",
                $"Work item is not in the required state for transition '{transitionKey}'.",
                $"İş kaydı '{transitionKey}' transition'ı için uygun state'te değil.",
                400);
        }
    }

    public static IReadOnlyList<string> GetRequiredFields(JsonElement transition)
    {
        if (!transition.TryGetProperty("requiredFields", out var fields)
            || fields.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var list = new List<string>();
        foreach (var item in fields.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var s = item.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    list.Add(s);
            }
        }

        return list;
    }

    public static void EnsureRequiredFields(
        JsonElement transition,
        IReadOnlyDictionary<string, object?> workItem)
    {
        foreach (var field in GetRequiredFields(transition))
        {
            var value = WorkItemDataHelper.GetFieldValue(workItem, field);
            if (IsEmptyValue(value))
            {
                throw new OperationCoreException(
                    "TRANSITION_REQUIRED_FIELD",
                    $"Field '{field}' is required for this transition.",
                    $"Bu transition için '{field}' alanı zorunludur.",
                    400);
            }
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
            _ => false
        };

    public static string? GetStringProperty(JsonElement element, string name) => GetString(element, name);

    private static string? GetString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var prop))
            return null;

        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
    }
}
