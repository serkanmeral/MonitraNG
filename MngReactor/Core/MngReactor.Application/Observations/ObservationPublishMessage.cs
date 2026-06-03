using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MngReactor.Application.Observations;

/// <summary>
/// Flat observation payload and routing key for <c>monitra.observations</c>.
/// Contract aligned with MngAlarm MetricObservationMapper (monitra.observations routing).
/// </summary>
public static class ObservationPublishMessage
{
    public const string ExchangeName = "monitra.observations";

    public static string BuildRoutingKey(string domainId, string collectibleCode) =>
        $"{domainId.Trim()}.metric.{collectibleCode.Trim()}";

    public static string SerializeFlatPayload(
        string domainId,
        string domainName,
        string collectibleCode,
        double value,
        IReadOnlyDictionary<string, string?>? dimensions = null,
        DateTime? timestamp = null)
    {
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["domainName"] = domainName.Trim(),
            ["domainId"] = domainId.Trim(),
            ["collectibleCode"] = collectibleCode.Trim(),
            ["value"] = value
        };

        if (timestamp.HasValue)
            payload["timestamp"] = timestamp.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

        if (dimensions != null)
        {
            foreach (var (key, val) in dimensions)
            {
                if (!string.IsNullOrWhiteSpace(val))
                    payload[key] = val;
            }
        }

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    public static string SerializeNestedMetaPayload(
        string domainName,
        double value,
        string collectibleCode,
        IReadOnlyDictionary<string, string?>? dimensions = null,
        DateTime? timestamp = null)
    {
        var meta = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["domain"] = domainName.Trim(),
            ["collectibleCode"] = collectibleCode.Trim()
        };

        if (dimensions != null)
        {
            foreach (var name in new[] { "assetId", "itemId", "agentId", "engineId", "unit" })
            {
                if (dimensions.TryGetValue(name, out var val) && !string.IsNullOrWhiteSpace(val))
                    meta[name] = val;
            }
        }

        var payload = new NestedMetaPayload
        {
            DomainName = domainName.Trim(),
            Value = value,
            Timestamp = timestamp?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            Meta = meta
        };

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private sealed class NestedMetaPayload
    {
        [JsonPropertyName("domainName")]
        public string DomainName { get; init; } = string.Empty;

        [JsonPropertyName("value")]
        public double Value { get; init; }

        [JsonPropertyName("timestamp")]
        public string? Timestamp { get; init; }

        [JsonPropertyName("meta")]
        public Dictionary<string, string?> Meta { get; init; } = new(StringComparer.Ordinal);
    }
}
