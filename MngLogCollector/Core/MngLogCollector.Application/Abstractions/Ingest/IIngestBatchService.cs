using MngLogCollector.Application.Contracts.Ingest;

namespace MngLogCollector.Application.Abstractions.Ingest;

public interface IIngestBatchService
{
    Task<IngestBatchResponse> IngestAsync(IngestBatchRequest request, CancellationToken cancellationToken = default);
}
