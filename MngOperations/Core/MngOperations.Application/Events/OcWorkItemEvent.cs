namespace MngOperations.Application.Events;

/// <summary>
/// Domain event published to oc.events (Q11).
/// </summary>
public sealed class OcWorkItemEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public required string DomainId { get; init; }
    public required string DomainName { get; init; }
    public required string EventType { get; init; }
    public string? WorkspaceId { get; init; }
    public string? WorkItemId { get; init; }
    public string? WorkItemKey { get; init; }
    public string? TransitionKey { get; init; }
    public string? Actor { get; init; }
}
