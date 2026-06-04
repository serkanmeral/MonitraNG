using MngEngine.Application.Features.SecEvents;

namespace MngEngine.Application.Interfaces;

/// <summary>Reactor sec-events ingest endpoint'ine batch gönderir.</summary>
public interface ISecEventIngestClient
{
    Task<SecEventIngestResult> SendAsync(SecEventIngestRequest request, CancellationToken ct = default);
}
