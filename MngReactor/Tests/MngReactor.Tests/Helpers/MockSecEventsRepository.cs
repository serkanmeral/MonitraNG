using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Tests.Helpers;

/// <summary>
/// Integration testlerinde gercek MongoDB olmadan sec-events ingest controller'ini calistirmak icin mock.
/// </summary>
public sealed class MockSecEventsRepository : ISecEventsRepository
{
    public Task<int> InsertManyAsync(
        string domain,
        IReadOnlyList<SecEventDocument> docs,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(docs?.Count ?? 0);

    public Task<SecEventQueryResult> QueryAsync(
        string domain,
        SecEventQueryFilter filter,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new SecEventQueryResult { Items = Array.Empty<SecEventListItem>(), Total = 0 });

    public Task<SecEventListItem?> GetByIdAsync(
        string domain,
        string id,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<SecEventListItem?>(null);
}
