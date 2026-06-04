namespace MngReactor.Application.Models.SecEvents;

/// <summary>Parser çıktısı — normalize edilmiş güvenlik olayı alanları.</summary>
public sealed class ParsedSecEvent
{
    public required DateTime Timestamp { get; init; }
    public required string EventAction { get; init; }
    public string? EventOutcome { get; init; }
    public string? EventCode { get; init; }
    public string? ActorUser { get; init; }
    public string? NetworkSrcIp { get; init; }
    public string? NetworkDstIp { get; init; }
    public int? NetworkDstPort { get; init; }
    public string? NetworkProtocol { get; init; }
    public required string SourceType { get; init; }
    public required string SourceProduct { get; init; }
    public string? SourceHost { get; init; }
    public required string ParserId { get; init; }
    public required string Raw { get; init; }
    public required string RawPreview { get; init; }

    public ParsedSecEvent WithEventAction(string eventAction) =>
        new()
        {
            Timestamp = Timestamp,
            EventAction = eventAction,
            EventOutcome = EventOutcome,
            EventCode = EventCode,
            ActorUser = ActorUser,
            NetworkSrcIp = NetworkSrcIp,
            NetworkDstIp = NetworkDstIp,
            NetworkDstPort = NetworkDstPort,
            NetworkProtocol = NetworkProtocol,
            SourceType = SourceType,
            SourceProduct = SourceProduct,
            SourceHost = SourceHost,
            ParserId = ParserId,
            Raw = Raw,
            RawPreview = RawPreview
        };
}
