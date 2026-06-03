using System.Globalization;
using System.Text.Json;

namespace MngAlarm.Application.Observations;

/// <summary>
/// Maps MngReactor metric RabbitMQ payloads to unified <see cref="ObservationEnvelope"/>.
/// Supports flat ingest DTO and nested mon_metrics-style meta object.
/// </summary>
public static class MetricObservationMapper
{
    public static ObservationEnvelope? TryMap(ReadOnlySpan<byte> body)
    {
        using var doc = JsonDocument.Parse(body.ToArray());
        return TryMap(doc.RootElement);
    }

    public static ObservationEnvelope? TryMap(JsonElement root)
    {
        var domainName = ReadString(root, "domainName")
            ?? ReadString(root, "domain")
            ?? ReadNestedString(root, "meta", "domain");

        if (string.IsNullOrWhiteSpace(domainName))
            return null;

        var key = ReadString(root, "collectibleCode")
            ?? ReadNestedString(root, "meta", "collectibleCode");

        if (string.IsNullOrWhiteSpace(key))
            return null;

        if (!TryReadDouble(root, "value", out var value))
            return null;

        var domainId = ReadString(root, "domainId") ?? domainName.Trim();
        var timestamp = ReadTimestamp(root);

        var dimensions = new Dictionary<string, object?>(StringComparer.Ordinal);
        CopyDimension(root, dimensions, "assetId");
        CopyDimension(root, dimensions, "itemId");
        CopyDimension(root, dimensions, "agentId");
        CopyDimension(root, dimensions, "engineId");
        CopyDimension(root, dimensions, "unit");

        if (root.TryGetProperty("meta", out var meta) && meta.ValueKind == JsonValueKind.Object)
        {
            CopyDimension(meta, dimensions, "assetId");
            CopyDimension(meta, dimensions, "itemId");
            CopyDimension(meta, dimensions, "agentId");
            CopyDimension(meta, dimensions, "engineId");
            CopyDimension(meta, dimensions, "unit");
        }

        return new ObservationEnvelope
        {
            DomainId = domainId,
            DomainName = domainName.Trim(),
            Kind = "metric",
            Key = key.Trim(),
            Value = value,
            Timestamp = timestamp,
            Dimensions = dimensions
        };
    }

    public static string BuildRoutingKey(ObservationEnvelope envelope) =>
        $"{envelope.DomainId}.metric.{envelope.Key}";

    private static void CopyDimension(JsonElement source, Dictionary<string, object?> target, string name)
    {
        var value = ReadString(source, name);
        if (!string.IsNullOrWhiteSpace(value))
            target[name] = value;
    }

    private static string? ReadNestedString(JsonElement root, string objectName, string propertyName)
    {
        if (!root.TryGetProperty(objectName, out var obj) || obj.ValueKind != JsonValueKind.Object)
            return null;

        return ReadString(obj, propertyName);
    }

    private static string? ReadString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var prop))
            return null;

        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString(),
            JsonValueKind.Number => prop.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
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

        if (prop.ValueKind == JsonValueKind.String &&
            double.TryParse(prop.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            return true;

        return false;
    }

    private static DateTime ReadTimestamp(JsonElement root)
    {
        if (root.TryGetProperty("timestamp", out var ts))
        {
            if (ts.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(ts.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
                return parsed.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc) : parsed.ToUniversalTime();

            if (ts.ValueKind == JsonValueKind.Number && ts.TryGetInt64(out var epochMs))
                return DateTimeOffset.FromUnixTimeMilliseconds(epochMs).UtcDateTime;
        }

        return DateTime.UtcNow;
    }
}
