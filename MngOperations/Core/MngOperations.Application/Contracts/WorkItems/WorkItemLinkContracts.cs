namespace MngOperations.Application.Contracts.WorkItems;

public sealed class CreateWorkItemLinkRequest
{
    public required string TargetWorkItemId { get; init; }
    public required string LinkType { get; init; }
    public string? Description { get; init; }
}

public sealed class WorkItemLinkDto
{
    public required string Id { get; init; }
    public required string SourceWorkItemId { get; init; }
    public required string TargetWorkItemId { get; init; }
    public required string LinkType { get; init; }
    public string? Description { get; init; }
}
