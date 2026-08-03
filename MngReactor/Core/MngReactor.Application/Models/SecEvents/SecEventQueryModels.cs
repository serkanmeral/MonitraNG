namespace MngReactor.Application.Models.SecEvents;

public sealed class SecEventQueryFilter
{
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public string? SourceType { get; init; }
    /// <summary>Exact match on source.product.</summary>
    public string? SourceProduct { get; init; }
    public string? EventAction { get; init; }
    /// <summary>Comma-separated event.action values (OR). Ignored when <see cref="EventAction"/> is set.</summary>
    public string? EventActions { get; init; }
    /// <summary>Prefix match on event.action (e.g. "rdp."). Ignored when EventAction/EventActions set.</summary>
    public string? EventActionPrefix { get; init; }
    /// <summary>Exact match on event.outcome (success | failure | …).</summary>
    public string? EventOutcome { get; init; }
    public string? SrcIp { get; init; }
    public string? DstIp { get; init; }
    /// <summary>Exact match on network.dstPort (string or numeric stored as string).</summary>
    public string? DstPort { get; init; }
    public string? ActorUser { get; init; }
    /// <summary>Case-insensitive contains on source.host.</summary>
    public string? SourceHost { get; init; }
    /// <summary>Comma-separated source.host values (OR contains). Ignored when <see cref="SourceHost"/> is set.</summary>
    public string? SourceHosts { get; init; }
    /// <summary>Exact match on event.code (e.g. Windows Event ID).</summary>
    public string? EventCode { get; init; }
    /// <summary>Comma-separated event.code values (OR). Ignored when <see cref="EventCode"/> is set.</summary>
    public string? EventCodes { get; init; }
    public string? Search { get; init; }
    /// <summary>Varsayılan true — bilinmeyen (event.action=unknown) olayları listeden çıkar.</summary>
    public bool ExcludeUnknown { get; init; } = true;
    public int Skip { get; init; }
    public int Limit { get; init; } = 50;
}

public sealed class SecEventQueryResult
{
    public required IReadOnlyList<SecEventListItem> Items { get; init; }
    public long Total { get; init; }
}

public sealed class SecEventListItem
{
    public required string Id { get; init; }
    public required DateTime Timestamp { get; init; }
    public required DateTime IngestedAt { get; init; }
    public string? SourceType { get; init; }
    public string? SourceProduct { get; init; }
    public string? SourceHost { get; init; }
    public required string EventAction { get; init; }
    public string? EventOutcome { get; init; }
    public string? EventCode { get; init; }
    public string? ActorUser { get; init; }
    public string? NetworkSrcIp { get; init; }
    public string? NetworkDstIp { get; init; }
    public string? ParserId { get; init; }
    public string? RawPreview { get; init; }
    /// <summary>Tam ham mesaj — yalnızca GET by id yanıtında dolu.</summary>
    public string? Raw { get; init; }
    public bool BaselineNewFlowPair { get; init; }
    /// <summary>Collector metric enrichment (e.g. host.up primaryIp / sessions). Null when absent.</summary>
    public IReadOnlyDictionary<string, object?>? Fields { get; init; }
}
