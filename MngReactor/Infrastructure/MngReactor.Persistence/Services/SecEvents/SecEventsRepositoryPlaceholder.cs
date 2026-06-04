using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Persistence.Services.SecEvents;

/// <summary>PR-1 iskelet — PR-3'te Mongo implementasyonu.</summary>
public sealed class SecEventsRepositoryPlaceholder : ISecEventsRepository
{
    public Task<int> InsertManyAsync(
        string domain,
        IReadOnlyList<SecEventDocument> docs,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(0);
}
