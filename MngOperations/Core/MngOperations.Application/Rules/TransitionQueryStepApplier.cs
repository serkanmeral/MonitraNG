using System.Text.Json;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Utilities;

namespace MngOperations.Application.Rules;

/// <summary>
/// Maps one named-query row onto work-item fields. Parameters are not read here.
/// </summary>
public static class TransitionQueryStepApplier
{
    public static Dictionary<string, object?> BuildMutations(JsonElement step, IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    {
        if (!step.TryGetProperty("mappings", out var mappings) || mappings.ValueKind != JsonValueKind.Array)
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        var first = rows.Count > 0 ? rows[0] : null;
        var mutations = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var mapping in mappings.EnumerateArray())
        {
            var to = ReadString(mapping, "to");
            var from = ReadString(mapping, "from");
            if (string.IsNullOrWhiteSpace(to) || string.IsNullOrWhiteSpace(from))
                continue;

            var onEmpty = (ReadString(mapping, "onEmpty") ?? "skip").Trim().ToLowerInvariant();
            var hasValue = false;
            object? value = null;
            if (first != null && TryGetRowValue(first, from, out value))
                hasValue = !IsEmpty(value);

            if (!hasValue)
            {
                switch (onEmpty)
                {
                    case "zero":
                        mutations[to] = 0;
                        break;
                    case "false":
                        mutations[to] = false;
                        break;
                    case "fail":
                        throw new OperationCoreException(
                            "QUERY_EMPTY",
                            $"Query returned no value for '{from}'.",
                            $"Sorgu '{from}' için değer döndürmedi.",
                            400);
                    default:
                        break;
                }

                continue;
            }

            mutations[to] = value;
        }

        return mutations;
    }

    public static Dictionary<string, object?> ResolveParameters(
        JsonElement step,
        IReadOnlyDictionary<string, object?> workItem)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (!step.TryGetProperty("parameters", out var list) || list.ValueKind != JsonValueKind.Array)
            return parameters;

        foreach (var parameter in list.EnumerateArray())
        {
            var name = ReadString(parameter, "name");
            var from = ReadString(parameter, "from");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(from))
                continue;

            var value = Normalize(ResolveWorkItemValue(workItem, from));
            // Optional text parameters reject JSON null. An empty work-item field is omitted
            // so the query keeps its own default. False and zero are still sent.
            if (value is null || (value is string text && string.IsNullOrWhiteSpace(text)))
                continue;

            parameters[name] = value;
        }

        return parameters;
    }

    public static string? ReadString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var prop))
            return null;
        return prop.ValueKind == JsonValueKind.String ? prop.GetString()?.Trim() : prop.ToString().Trim();
    }

    private static object? ResolveWorkItemValue(IReadOnlyDictionary<string, object?> workItem, string path)
    {
        var key = path.StartsWith("fields.", StringComparison.OrdinalIgnoreCase)
            ? path["fields.".Length..]
            : path;
        return WorkItemDataHelper.GetFieldValue(workItem, key);
    }

    private static bool TryGetRowValue(IReadOnlyDictionary<string, object?> row, string name, out object? value)
    {
        if (row.TryGetValue(name, out value))
            return true;

        foreach (var pair in row)
        {
            if (string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static bool IsEmpty(object? value)
    {
        value = Normalize(value);
        return value switch
        {
            null => true,
            string s => string.IsNullOrWhiteSpace(s),
            _ => false
        };
    }

    private static object? Normalize(object? value)
    {
        if (value is not JsonElement element)
            return value;

        return element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when element.TryGetInt64(out var whole) => whole,
            JsonValueKind.Number => element.GetDouble(),
            _ => element.GetRawText()
        };
    }
}
