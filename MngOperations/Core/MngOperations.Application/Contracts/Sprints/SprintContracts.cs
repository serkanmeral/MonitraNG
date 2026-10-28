namespace MngOperations.Application.Contracts.Sprints;

public sealed class SprintDto
{
    public required string Id { get; init; }
    public required string WorkspaceId { get; init; }
    public required string Name { get; init; }
    public string? Goal { get; init; }
    public required string StartDate { get; init; }
    public required string EndDate { get; init; }
    public required string State { get; init; }
    public int ItemCount { get; init; }
}

public sealed class SprintItemDto
{
    public required string Id { get; init; }
    public required string Key { get; init; }
    public required string Title { get; init; }
    public string? StateId { get; init; }
}

public sealed class CreateSprintRequest
{
    public string? Name { get; set; }
    public string? Goal { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
}

public sealed class UpdateSprintRequest
{
    public string? Name { get; set; }
    public string? Goal { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
}

public sealed class SetSprintMembershipRequest
{
    public string? WorkItemId { get; set; }

    /// <summary>Boş veya null ise iş backlog'a döner.</summary>
    public string? SprintId { get; set; }
}
