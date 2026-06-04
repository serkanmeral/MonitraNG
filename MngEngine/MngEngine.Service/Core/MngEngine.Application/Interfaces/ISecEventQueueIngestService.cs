using MngEngine.Application.Features.SecEvents;

namespace MngEngine.Application.Interfaces;

/// <summary>Sec-event öğelerini in-memory kuyruğa alır (syslog / WEC batch).</summary>
public interface ISecEventQueueIngestService
{
    Task<SecEventWecBatchResponse> IngestWecBatchAsync(
        SecEventWecBatchRequest request,
        CancellationToken cancellationToken = default);
}
