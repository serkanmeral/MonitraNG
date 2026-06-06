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

    public Task<SecEventDashboardSummary> GetDashboardSummaryAsync(
        string domain,
        SecEventDashboardSummaryRequest request,
        CancellationToken cancellationToken = default)
    {
        var hours = Math.Clamp(request?.RangeHours ?? 24, 1, 168);
        var to = DateTime.UtcNow;
        var from = to.AddHours(-hours);
        var hourStarts = Enumerable.Range(0, hours)
            .Select(idx => to.AddHours(-(hours - 1 - idx)).AddHours(-1))
            .Select(d => DateTime.SpecifyKind(d, DateTimeKind.Utc))
            .ToList();

        return Task.FromResult(new SecEventDashboardSummary
        {
            Range = $"{hours}h",
            From = from,
            To = to,
            EventsTotal = 0,
            ByAction = new Dictionary<string, long>(),
            Hourly = hourStarts.Select(h => new SecEventHourlyBucket { HourStart = h, Count = 0 }).ToList(),
        });
    }
}
