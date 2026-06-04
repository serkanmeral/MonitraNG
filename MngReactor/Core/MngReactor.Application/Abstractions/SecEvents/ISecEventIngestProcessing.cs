using MngReactor.Application.Features.Commands.Ingest;
using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Application.Abstractions.SecEvents;

public interface ISecEventIngestProcessing
{
    Task<SecEventIngestResponse> ProcessAsync(
        SecEventIngestRequest request,
        string domainFromToken,
        string? accessToken = null,
        CancellationToken cancellationToken = default);
}
