namespace MngOperations.Application.Contracts.WorkItems;

public sealed class CreateWorkItemResponse
{
    /// <summary>
    /// Örn. ALREADY_EXISTS — from-origin idempotent tekrar istek.
    /// </summary>
    public string? Code { get; init; }

    public required WorkItemDto WorkItem { get; init; }
}

public sealed class WorkItemDto
{
    public required string Id { get; init; }
    public required string Key { get; init; }
    public required string WorkspaceId { get; init; }
    public string? WorkspaceKey { get; init; }
    public required string TypeId { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required string StateId { get; init; }
    public string? StateFlowId { get; init; }
    public required string Category { get; init; }
    public string? BoardId { get; init; }
    public string? Assignee { get; init; }
    public string? PriorityId { get; init; }
    public IReadOnlyDictionary<string, object?>? Origin { get; init; }
    public IReadOnlyDictionary<string, object?>? Fields { get; init; }
}
