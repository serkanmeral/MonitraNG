using System.Text.Json;

namespace MngOperations.Application.Contracts.WorkItems;

public sealed class PatchWorkItemRequest
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? Assignee { get; init; }
    public string? PriorityId { get; init; }
    public string? BoardId { get; init; }
    public JsonElement? Fields { get; init; }
}
