namespace MngReactor.Application.Features.Commands.Ingest;

/// <summary>
/// Ingest metrics request - Engine'den gelen batch array
/// </summary>
public record IngestMetricsRequest
{
    public required List<IngestBatch> Batches { get; init; }
}

public record IngestBatch
{
    public required string AssetId { get; init; }
    public string? ItemId { get; init; }
    public required string AgentId { get; init; }
    public required string EngineId { get; init; }
    public required DateTime CollectedAt { get; init; }
    public required List<IngestMetric> Metrics { get; init; }
}

public record IngestMetric
{
    public required string CollectibleCode { get; init; }
    public required object Value { get; init; }
    public string? Unit { get; init; }
}
