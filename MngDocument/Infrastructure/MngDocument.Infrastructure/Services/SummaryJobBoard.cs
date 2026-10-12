using System.Collections.Concurrent;

namespace MngDocument.Infrastructure.Services;

/// <summary>
/// Live summary progress for the process. Polling reads this first so a failed
/// Data Gateway write cannot leave the dialog on a stale "running" step.
/// </summary>
public sealed class SummaryJobBoard
{
    private readonly ConcurrentDictionary<string, SummaryJobSnapshot> _jobs = new(StringComparer.Ordinal);

    public void Set(string id, SummaryJobSnapshot snapshot) => _jobs[id] = snapshot;

    public bool TryGet(string id, out SummaryJobSnapshot snapshot) => _jobs.TryGetValue(id, out snapshot!);

    public void Remove(string id) => _jobs.TryRemove(id, out _);
}

public sealed class SummaryJobSnapshot
{
    public string Status { get; init; } = "none";
    public string? Text { get; init; }
    public string? Error { get; init; }
    public string? Step { get; init; }
    public bool Chunked { get; init; }
    public DateTime? SummarizedAt { get; init; }
    public int Completed { get; init; }
    public int Total { get; init; }
    public DateTime UpdatedAt { get; init; }
    public IReadOnlyList<string> Log { get; init; } = Array.Empty<string>();
}
