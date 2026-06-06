namespace MngReactor.Application.Models.SecEvents;

public sealed class SecEventDashboardSummaryRequest
{
    /// <summary>Sliding window length in hours (default 24).</summary>
    public int RangeHours { get; init; } = 24;

    public bool ExcludeUnknown { get; init; } = true;
}

public sealed class SecEventHourlyBucket
{
    public required DateTime HourStart { get; init; }
    public long Count { get; init; }
}

public sealed class SecEventDashboardSummary
{
    public required string Range { get; init; }
    public required DateTime From { get; init; }
    public required DateTime To { get; init; }
    public long EventsTotal { get; init; }
    public required IReadOnlyDictionary<string, long> ByAction { get; init; }
    public required IReadOnlyList<SecEventHourlyBucket> Hourly { get; init; }
}
