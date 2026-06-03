using MngReactor.Application.Features.Commands.Ingest;

namespace MngReactor.Application.Abstractions.Ingest;

public interface IIngestProcessing
{
    Task<IngestMetricsResponse> ProcessAsync(IngestMetricsRequest request, string domainFromToken, string? accessToken = null, CancellationToken cancellationToken = default);
}
