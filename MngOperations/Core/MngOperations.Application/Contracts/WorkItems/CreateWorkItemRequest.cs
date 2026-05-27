using System.Text.Json;

namespace MngOperations.Application.Contracts.WorkItems;

public sealed class CreateWorkItemRequest
{
    public required string WorkspaceId { get; init; }
    public required string TypeId { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public JsonElement? Fields { get; init; }
    public string? BoardId { get; init; }
    public string? Assignee { get; init; }
    public string? PriorityId { get; init; }
}
