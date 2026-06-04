using System.Text.Json;

namespace MngAlarm.Application.Observations;

/// <summary>
/// Parses inbound observation queue messages (native Reactor flat DTO or ObservationEnvelope).
/// </summary>
public static class ObservationIngressParser
{
    public static ObservationEnvelope? TryParse(ReadOnlySpan<byte> body)
    {
        var mapped = MetricObservationMapper.TryMap(body);
        if (mapped != null)
            return mapped;

        var eventMapped = EventObservationMapper.TryMap(body);
        if (eventMapped != null)
            return eventMapped;

        return JsonSerializer.Deserialize<ObservationEnvelope>(body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }
}
