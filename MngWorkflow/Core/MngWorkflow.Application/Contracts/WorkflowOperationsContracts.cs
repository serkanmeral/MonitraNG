using System.Text.Json;

namespace MngWorkflow.Application.Contracts;

public sealed class WorkflowCreateFromOriginRequest
{
    public required string WorkspaceId { get; init; }
    public required string TypeId { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public JsonElement? Fields { get; init; }
    public string? BoardId { get; init; }
    public string? Assignee { get; init; }
    public string? PriorityId { get; init; }
    public required WorkflowWorkItemOriginRequest Origin { get; init; }
    public string? InitialTransitionKey { get; init; }
}

public sealed class WorkflowWorkItemOriginRequest
{
    public required string SourceType { get; init; }
    public required string SourceId { get; init; }
    public required string CorrelationId { get; init; }
    public string? SourceSystem { get; init; }
    public JsonElement? Payload { get; init; }
}

public sealed class WorkflowCreateWorkItemResponse
{
    public string? Code { get; init; }
    public required WorkflowWorkItemDto WorkItem { get; init; }
}

public sealed class WorkflowWorkItemDto
{
    public required string Id { get; init; }
    public required string Key { get; init; }
    public required string WorkspaceId { get; init; }
    public required string TypeId { get; init; }
    public required string Title { get; init; }
    public required string StateId { get; init; }
}

public sealed class WorkflowTransitionWorkItemRequest
{
    public JsonElement? Fields { get; init; }
    public string? Comment { get; init; }
}

public sealed class WorkflowTransitionWorkItemResponse
{
    public required WorkflowWorkItemDto WorkItem { get; init; }
    public IReadOnlyList<WorkflowAvailableTransitionDto> AvailableTransitions { get; init; } = [];
}

public sealed class WorkflowPatchWorkItemRequest
{
    public string? Title { get; init; }
    public JsonElement? Description { get; init; }
    public JsonElement? Assignee { get; init; }
    public JsonElement? PriorityId { get; init; }
    public JsonElement? BoardId { get; init; }
    public JsonElement? Fields { get; init; }
}

public sealed class WorkflowAvailableTransitionDto
{
    public required string TransitionKey { get; init; }
    public string? Label { get; init; }
    public required string FromStateId { get; init; }
    public required string ToStateId { get; init; }
}
