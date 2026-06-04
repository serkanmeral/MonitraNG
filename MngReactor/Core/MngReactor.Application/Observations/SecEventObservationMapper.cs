using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Application.Observations;

/// <summary>
/// Maps persisted <see cref="SecEventDocument"/> to MngAlarm <c>ObservationEnvelope</c> fields.
/// Contract: <see href="docs/odak/monitoring/SEC_EVENT_OBSERVATION_MAP.md"/>.
/// </summary>
public static class SecEventObservationMapper
{
    public static SecEventObservationPayload ToPayload(SecEventDocument document, string domainId, string domainName)
    {
        var resolvedDomainId = string.IsNullOrWhiteSpace(domainId) ? domainName.Trim() : domainId.Trim();
        var dimensions = new Dictionary<string, object?>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(document.Actor?.User))
            dimensions["userId"] = document.Actor.User.Trim();

        if (!string.IsNullOrWhiteSpace(document.Network?.SrcIp))
            dimensions["srcIp"] = document.Network.SrcIp.Trim();

        if (!string.IsNullOrWhiteSpace(document.Network?.DstIp))
            dimensions["dstIp"] = document.Network.DstIp.Trim();

        if (document.Network?.DstPort is int dstPort)
            dimensions["dstPort"] = dstPort;

        if (!string.IsNullOrWhiteSpace(document.Source.Type))
            dimensions["sourceType"] = document.Source.Type.Trim();

        if (!string.IsNullOrWhiteSpace(document.Source.Host))
            dimensions["sourceHost"] = document.Source.Host.Trim();

        if (!string.IsNullOrWhiteSpace(document.Event.Outcome))
            dimensions["eventOutcome"] = document.Event.Outcome.Trim();

        if (!string.IsNullOrWhiteSpace(document.Parser.Id))
            dimensions["parserId"] = document.Parser.Id.Trim();

        if (!string.IsNullOrWhiteSpace(document.Event.Code))
            dimensions["eventCode"] = document.Event.Code.Trim();

        return new SecEventObservationPayload
        {
            DomainId = resolvedDomainId,
            DomainName = domainName.Trim(),
            Kind = "event",
            Key = document.Event.Action.Trim(),
            Value = 1,
            Timestamp = document.Timestamp,
            Dimensions = dimensions
        };
    }
}

public sealed class SecEventObservationPayload
{
    public required string DomainId { get; init; }
    public required string DomainName { get; init; }
    public required string Kind { get; init; }
    public required string Key { get; init; }
    public double Value { get; init; }
    public required DateTime Timestamp { get; init; }
    public IReadOnlyDictionary<string, object?> Dimensions { get; init; } =
        new Dictionary<string, object?>(StringComparer.Ordinal);
}
