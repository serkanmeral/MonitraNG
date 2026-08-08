using System.Text.Json;

namespace MngAlarm.Application.Observations;

/// <summary>
/// API JSON deserialization leaves dimension values as <see cref="JsonElement"/>;
/// MongoDB ObjectSerializer rejects them unless converted to primitives.
/// Arrays/objects are expanded (not GetRawText) so condition operators like <c>in</c> keep working.
/// </summary>
public static class ObservationValueNormalizer
{
    public static Dictionary<string, object?> NormalizeDimensions(IReadOnlyDictionary<string, object?>? dimensions)
    {
        if (dimensions is null || dimensions.Count == 0)
            return new Dictionary<string, object?>(StringComparer.Ordinal);

        var result = new Dictionary<string, object?>(dimensions.Count, StringComparer.Ordinal);
        foreach (var (key, value) in dimensions)
            result[key] = Normalize(value);

        return result;
    }

    public static object? Normalize(object? value)
    {
        if (value is null)
            return null;

        if (value is JsonElement element)
            return NormalizeJsonElement(element);

        return value;
    }

    private static object? NormalizeJsonElement(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.Array => element.EnumerateArray()
                .Select(NormalizeJsonElement)
                .ToList(),
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(
                    prop => prop.Name,
                    prop => NormalizeJsonElement(prop.Value),
                    StringComparer.Ordinal),
            _ => element.GetRawText()
        };
}
