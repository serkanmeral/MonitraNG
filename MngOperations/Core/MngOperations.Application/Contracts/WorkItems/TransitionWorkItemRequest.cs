using System.Text.Json;

namespace MngOperations.Application.Contracts.WorkItems;

public sealed class TransitionWorkItemRequest
{
    public JsonElement? Fields { get; init; }
    public string? Comment { get; init; }
}

public sealed class TransitionWorkItemResponse
{
    public required WorkItemDto WorkItem { get; init; }
    public IReadOnlyList<AvailableTransitionDto> AvailableTransitions { get; init; } = Array.Empty<AvailableTransitionDto>();
}

public sealed class AvailableTransitionDto
{
    public required string TransitionKey { get; init; }
    public string? Label { get; init; }
    public required string FromStateId { get; init; }
    public required string ToStateId { get; init; }
}
