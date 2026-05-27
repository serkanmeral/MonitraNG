using System.Text.Json;

namespace MngOperations.Application.Contracts.Runtime;

public sealed class BoardRuntimeContext
{
    public required string BoardId { get; init; }
    public required string WorkspaceId { get; init; }
    public string? Name { get; init; }
    public string? ViewType { get; init; }
    public RuntimePermissionsDto Permissions { get; init; } = new();
    public IReadOnlyList<BoardColumnDto> Columns { get; init; } = Array.Empty<BoardColumnDto>();
    public IReadOnlyList<string> CardFieldKeys { get; init; } = Array.Empty<string>();
}

public sealed class BoardColumnDto
{
    public required string StateId { get; init; }
    public string? Title { get; init; }
    public bool DropEligible { get; init; } = true;
    public string? DefaultTransitionKey { get; init; }
    public IReadOnlyList<string> AlternativeTransitionKeys { get; init; } = Array.Empty<string>();
    public required string QueryKey { get; init; }
    public IReadOnlyDictionary<string, string> ParametersTemplate { get; init; }
        = new Dictionary<string, string>();
    public int SuggestedPageSize { get; init; } = 50;
}

public sealed class ExecuteQueryRequest
{
    public string Dataset { get; init; } = "op_work_items";
    public Dictionary<string, JsonElement>? Parameters { get; init; }
    public int Skip { get; init; }
    public int Take { get; init; } = 50;
}

public sealed class QueryExecuteResponse
{
    public required string Dataset { get; init; }
    public required string QueryKey { get; init; }
    public IReadOnlyList<WorkItemCardDto> Items { get; init; } = Array.Empty<WorkItemCardDto>();
    public int Skip { get; init; }
    public int Take { get; init; }
    public int Total { get; init; }
}

public sealed class WorkItemCardDto
{
    public required string Id { get; init; }
    public required string Key { get; init; }
    public required string Title { get; init; }
    public string? StateId { get; init; }
    public string? Assignee { get; init; }
    public string? PriorityId { get; init; }
    public string? TypeId { get; init; }
}
