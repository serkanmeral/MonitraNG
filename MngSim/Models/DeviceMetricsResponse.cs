using System.Text.Json.Serialization;

namespace MngSim.Models;

/// <summary>
/// HTTP /metrics endpoint yanıtı — Engine HTTP collector ile uyumlu.
/// </summary>
public class DeviceMetricsResponse
{
    [JsonPropertyName("collectedAt")]
    public DateTime CollectedAt { get; set; }

    [JsonPropertyName("deviceId")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("metrics")]
    public List<MetricItem> Metrics { get; set; } = new();
}

public class MetricItem
{
    [JsonPropertyName("collectibleCode")]
    public string CollectibleCode { get; set; } = "";

    [JsonPropertyName("value")]
    public object Value { get; set; } = null!;

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }
}
