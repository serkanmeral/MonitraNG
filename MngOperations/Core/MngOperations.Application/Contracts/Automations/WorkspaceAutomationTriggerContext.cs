namespace MngOperations.Application.Contracts.Automations;

public sealed class WorkspaceAutomationTriggerContext
{
    public required string EventName { get; init; }
    public required string WorkspaceId { get; init; }
    public string? BoardId { get; init; }
    public string? TypeId { get; init; }
    public required string WorkItemId { get; init; }
    public required string WorkItemKey { get; init; }
    public string? ToStateId { get; init; }
    public string? FromStateId { get; init; }
    public string? TransitionKey { get; init; }
    public required IReadOnlyDictionary<string, object?> WorkItem { get; init; }
}
