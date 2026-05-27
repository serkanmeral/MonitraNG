namespace MngOperations.Application.Contracts.Runtime;

public sealed class SlaSnapshotDto
{
    public string? SlaPolicyId { get; init; }
    public DateTime? ResponseDueAt { get; init; }
    public DateTime? ResolveDueAt { get; init; }
    public bool ResponseBreached { get; init; }
    public bool ResolveBreached { get; init; }
    public DateTime? CalculatedAt { get; init; }
}

public sealed class StateSegmentDto
{
    public string? Id { get; init; }
    public string? FromStateId { get; init; }
    public required string ToStateId { get; init; }
    public DateTime EnteredAt { get; init; }
    public DateTime? LeftAt { get; init; }
    public long? DurationMs { get; init; }
    public string? TransitionKey { get; init; }
    public string? ChangedBy { get; init; }
    public string? AssigneeAtThatTime { get; init; }
}

public sealed class StateSegmentsPage
{
    public IReadOnlyList<StateSegmentDto> Items { get; init; } = Array.Empty<StateSegmentDto>();
    public int Total { get; init; }
}
