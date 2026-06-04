using System.Globalization;
using System.Text.Json;

namespace MngAlarm.Application.Observations;

/// <summary>
/// Maps MngReactor sec_event observation payloads (kind=event) to <see cref="ObservationEnvelope"/>.
/// </summary>
public static class EventObservationMapper
{
    public static ObservationEnvelope? TryMap(ReadOnlySpan<byte> body)
    {
        using var doc = JsonDocument.Parse(body.ToArray());
        return TryMap(doc.RootElement);
    }

    public static ObservationEnvelope? TryMap(JsonElement root)
    {
        var kind = ReadString(root, "kind");
        if (!string.Equals(kind, "event", StringComparison.OrdinalIgnoreCase))
            return null;

        var domainName = ReadString(root, "domainName")
            ?? ReadString(root, "domain");
        var key = ReadString(root, "key");

        if (string.IsNullOrWhiteSpace(domainName) || string.IsNullOrWhiteSpace(key))
            return null;

        var domainId = ReadString(root, "domainId") ?? domainName.Trim();
        var timestamp = ReadTimestamp(root);
        var dimensions = ReadDimensions(root);

        double? value = null;
        if (TryReadDouble(root, "value", out var parsedValue))
            value = parsedValue;

        return new ObservationEnvelope
        {
            DomainId = domainId,
            DomainName = domainName.Trim(),
            Kind = "event",
            Key = key.Trim(),
            Value = value,
            Timestamp = timestamp,
            Dimensions = dimensions
        };
    }

    public static string BuildRoutingKey(ObservationEnvelope envelope) =>
        $"{envelope.DomainId}.event.{envelope.Key}";

    private static Dictionary<string, object?> ReadDimensions(JsonElement root)
    {
        var dimensions = new Dictionary<string, object?>(StringComparer.Ordinal);

        if (!root.TryGetProperty("dimensions", out var dims) || dims.ValueKind != JsonValueKind.Object)
            return dimensions;

        foreach (var prop in dims.EnumerateObject())
        {
            dimensions[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString(),
                JsonValueKind.Number when prop.Value.TryGetInt32(out var i) => i,
                JsonValueKind.Number when prop.Value.TryGetDouble(out var d) => d,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => prop.Value.GetRawText()
            };
        }

        return dimensions;
    }

    private static string? ReadString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var prop))
            return null;

        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString(),
            JsonValueKind.Number => prop.GetRawText(),
            _ => null
        };
    }

    private static bool TryReadDouble(JsonElement root, string name, out double value)
    {
        value = default;
        if (!root.TryGetProperty(name, out var prop))
            return false;

        if (prop.ValueKind == JsonValueKind.Number)
            return prop.TryGetDouble(out value);

        return prop.ValueKind == JsonValueKind.String
               && double.TryParse(prop.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static DateTime ReadTimestamp(JsonElement root)
    {
        if (root.TryGetProperty("timestamp", out var ts)
            && ts.ValueKind == JsonValueKind.String
            && DateTime.TryParse(ts.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            return parsed.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
                : parsed.ToUniversalTime();
        }

        return DateTime.UtcNow;
    }
}
