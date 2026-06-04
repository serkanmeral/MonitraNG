namespace MngReactor.Application.Models.SecEvents;

/// <summary>Mongo sec_events koleksiyon belgesi (flat alanlar, ≤ ~2 KB hedef).</summary>
public sealed class SecEventDocument
{
    public required DateTime Timestamp { get; init; }
    public required DateTime IngestedAt { get; init; }
    public required string Domain { get; init; }
    public required SecEventSourceInfo Source { get; init; }
    public required SecEventEventBlock Event { get; init; }
    public SecEventActorBlock? Actor { get; init; }
    public SecEventNetworkBlock? Network { get; init; }
    public required SecEventParserBlock Parser { get; init; }
    public required string RawPreview { get; init; }
}

public sealed class SecEventEventBlock
{
    public required string Action { get; init; }
    public string? Outcome { get; init; }
    public string? Code { get; init; }
}

public sealed class SecEventActorBlock
{
    public string? User { get; init; }
}

public sealed class SecEventNetworkBlock
{
    public string? SrcIp { get; init; }
    public string? DstIp { get; init; }
    public int? DstPort { get; init; }
    public string? Protocol { get; init; }
}

public sealed class SecEventParserBlock
{
    public required string Id { get; init; }
}
