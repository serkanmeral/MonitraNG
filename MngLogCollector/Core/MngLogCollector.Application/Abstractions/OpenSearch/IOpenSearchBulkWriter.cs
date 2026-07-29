namespace MngLogCollector.Application.Abstractions.OpenSearch;

public interface IOpenSearchBulkWriter
{
    Task<int> IndexSecEventsAsync(
        string domain,
        IReadOnlyList<OpenSearchSecEventDocument> documents,
        CancellationToken cancellationToken = default);
}

public sealed class OpenSearchSecEventDocument
{
    public required string Id { get; init; }
    public required DateTime IngestedAtUtc { get; init; }
    public required DateTime EventTimeUtc { get; init; }
    public required string HostId { get; init; }
    public string? Hostname { get; init; }
    public required string Source { get; init; }
    public string? SourceProduct { get; init; }
    public string? Severity { get; init; }
    public string? Message { get; init; }
    public string? RawPreview { get; init; }
    public Dictionary<string, object?>? Fields { get; init; }
    public string Collector { get; init; } = "mnglogcollector";
}
