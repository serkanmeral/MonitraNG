using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Features.Commands.Ingest;
using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Persistence.Services.SecEvents;

/// <summary>PR-1 iskelet — PR-4'te gerçek orchestrator ile değiştirilir.</summary>
public sealed class SecEventIngestProcessingPlaceholder : ISecEventIngestProcessing
{
    public Task<SecEventIngestResponse> ProcessAsync(
        SecEventIngestRequest request,
        string domainFromToken,
        string? accessToken = null,
        CancellationToken cancellationToken = default)
    {
        var itemCount = request.Items?.Count ?? 0;
        if (itemCount > SecEventIngestLimits.MaxItemsPerRequest)
        {
            return Task.FromResult(new SecEventIngestResponse
            {
                Accepted = 0,
                Rejected = itemCount,
                Published = 0,
                ImplementationPending = true,
                Message = $"Batch exceeds max items ({SecEventIngestLimits.MaxItemsPerRequest})."
            });
        }

        return Task.FromResult(new SecEventIngestResponse
        {
            Accepted = 0,
            Rejected = itemCount,
            Published = 0,
            ImplementationPending = true,
            Message = "sec_events ingest not implemented (PR-1 skeleton)."
        });
    }
}
