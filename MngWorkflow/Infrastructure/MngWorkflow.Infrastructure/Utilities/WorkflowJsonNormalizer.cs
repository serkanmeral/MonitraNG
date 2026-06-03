using System.Text.Json;
using MngWorkflow.Domain.Entities;

namespace MngWorkflow.Infrastructure.Utilities;

public static class WorkflowJsonNormalizer
{
    public static List<WorkflowNodeDefinition> NormalizeNodes(IReadOnlyList<WorkflowNodeDefinition> nodes) =>
        nodes.Select(n => new WorkflowNodeDefinition
        {
            Id = n.Id,
            Type = n.Type,
            Config = NormalizeDictionary(n.Config)
        }).ToList();

    public static Dictionary<string, object?> NormalizeDictionary(Dictionary<string, object?> source)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (key, value) in source)
            result[key] = NormalizeValue(value);
        return result;
    }

    public static object? NormalizeValue(object? value) =>
        value switch
        {
            null => null,
            JsonElement element => JsonElementToObject(element),
            Dictionary<string, object?> dict => NormalizeDictionary(dict),
            IDictionary<string, object?> dict => NormalizeDictionary(dict.ToDictionary(k => k.Key, k => k.Value)),
            _ => value
        };

    private static object? JsonElementToObject(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(p => p.Name, p => JsonElementToObject(p.Value)),
            JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToList(),
            _ => element.GetRawText()
        };
}
