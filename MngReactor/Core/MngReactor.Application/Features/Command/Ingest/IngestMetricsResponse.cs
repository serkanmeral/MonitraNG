namespace MngReactor.Application.Features.Commands.Ingest;

/// <summary>
/// Ingest metrics response - partial success destekler
/// </summary>
public record IngestMetricsResponse
{
    public int SavedCount { get; init; }
    public int FailedCount { get; init; }
    public List<IngestError> ErrorList { get; init; } = [];
}

public record IngestError
{
    public int BatchIndex { get; init; }
    public int MetricIndex { get; init; }
    public required string Code { get; init; }
    public required string Message { get; init; }
}
