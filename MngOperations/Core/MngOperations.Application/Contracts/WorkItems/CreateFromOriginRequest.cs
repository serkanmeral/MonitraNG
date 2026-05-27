namespace MngOperations.Application.Contracts.WorkItems;

public sealed class CreateFromOriginRequest
{
    public required string WorkspaceId { get; init; }
    public required string TypeId { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public System.Text.Json.JsonElement? Fields { get; init; }
    public string? BoardId { get; init; }
    public string? Assignee { get; init; }
    public string? PriorityId { get; init; }
    public required WorkItemOriginInput Origin { get; init; }

    /// <summary>
    /// Opsiyonel: create sonrası başlangıç state yerine bu transition'ın hedef state'i (katalogdan).
    /// </summary>
    public string? InitialTransitionKey { get; init; }
}
