using System.Text.Json;

namespace MngOperations.Application.Contracts.Runtime;

public sealed class DashboardRuntimeContext
{
    public required string DashboardId { get; init; }
    public string? WorkspaceId { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Scope { get; init; }
    public JsonElement? Layout { get; init; }
    public RuntimePermissionsDto Permissions { get; init; } = new();
    public IReadOnlyList<DashboardWidgetRuntimeDto> Widgets { get; init; } = Array.Empty<DashboardWidgetRuntimeDto>();
}

public sealed class DashboardWidgetRuntimeDto
{
    public required string Key { get; init; }
    public required string WidgetType { get; init; }
    public string? Title { get; init; }
    public string? Dataset { get; init; }
    public string? QueryKey { get; init; }
    public IReadOnlyDictionary<string, object?>? ResolvedParameters { get; init; }
    public DashboardWidgetExecutionDto? Execution { get; init; }
}

public sealed class DashboardWidgetExecutionDto
{
    public bool Success { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public int Total { get; init; }
    public int Skip { get; init; }
    public int Take { get; init; }
    public IReadOnlyList<WorkItemCardDto> Items { get; init; } = Array.Empty<WorkItemCardDto>();
    public DateTime ExecutedAt { get; init; }
}
