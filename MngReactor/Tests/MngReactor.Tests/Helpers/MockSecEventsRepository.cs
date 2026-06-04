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
}
