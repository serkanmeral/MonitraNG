using System.Text.Json;
using MngOperations.Application.Rules;

namespace MngOperations.Application.Utilities;

public static class RuleActionParser
{
    public static IReadOnlyList<JsonElement> ParseActions(JsonElement? actions)
    {
        if (actions is not { ValueKind: JsonValueKind.Array })
            return Array.Empty<JsonElement>();

        return actions.Value.EnumerateArray().ToList();
    }

    public static string? GetActionType(JsonElement action)
    {
        if (action.TryGetProperty("type", out var typeProp) && typeProp.ValueKind == JsonValueKind.String)
            return typeProp.GetString();

        return null;
    }

    public static string? GetString(JsonElement action, string name)
    {
        if (!action.TryGetProperty(name, out var prop))
            return null;

        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.ToString();
    }

    public static object? GetValue(JsonElement action, string name)
    {
        if (!action.TryGetProperty(name, out var prop))
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
