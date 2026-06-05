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
    public required string Raw { get; init; }
    public required string RawPreview { get; init; }
    /// <summary>U7: baseline sonrası ilk kez görülen src→dst çifti.</summary>
    public bool BaselineNewFlowPair { get; init; }

    public static SecEventDocument FromParsed(
        ParsedSecEvent parsed,
        string domain,
        DateTime ingestedAt,
        bool baselineNewFlowPair = false,
        bool persistFullRaw = false) =>
        new()
        {
            Timestamp = parsed.Timestamp,
            IngestedAt = ingestedAt,
            Domain = domain,
            BaselineNewFlowPair = baselineNewFlowPair,
            Source = new SecEventSourceInfo
            {
                Type = parsed.SourceType,
                Product = parsed.SourceProduct,
                Host = parsed.SourceHost
            },
            Event = new SecEventEventBlock
            {
                Action = parsed.EventAction,
                Outcome = parsed.EventOutcome,
                Code = parsed.EventCode
            },
            Actor = string.IsNullOrWhiteSpace(parsed.ActorUser)
                ? null
                : new SecEventActorBlock { User = parsed.ActorUser },
            Network = parsed.NetworkSrcIp is null
                       && parsed.NetworkDstIp is null
                       && parsed.NetworkDstPort is null
                       && parsed.NetworkProtocol is null
                ? null
                : new SecEventNetworkBlock
                {
                    SrcIp = parsed.NetworkSrcIp,
                    DstIp = parsed.NetworkDstIp,
                    DstPort = parsed.NetworkDstPort,
                    Protocol = parsed.NetworkProtocol
                },
            Parser = new SecEventParserBlock { Id = parsed.ParserId },
            Raw = persistFullRaw ? parsed.Raw : string.Empty,
            RawPreview = parsed.RawPreview
        };
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
