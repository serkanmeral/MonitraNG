using MngEngine.Application.Features.Ingest;

namespace MngEngine.Application.Interfaces;

/// <summary>
/// Toplanan batch'leri Reactor Ingest endpoint'ine gönderir.
/// </summary>
public interface IIngestClient
{
    /// <summary>
    /// Batch'leri şifreleyip/sıkıştırıp POST /api/v1/ingest/metrics ile gönderir.
    /// </summary>
    Task<IngestResult> SendAsync(IngestMetricsRequest request, CancellationToken ct = default);
}

public record IngestResult
{
    public bool Success { get; init; }
    public int SavedCount { get; init; }
    public int FailedCount { get; init; }
    public string? ErrorMessage { get; init; }
}
