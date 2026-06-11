using System.Text.Json;

namespace MngOperations.Application.Contracts.Runtime;

public sealed class FormRuntimeContext
{
    public required string Mode { get; init; }
    public required string WorkspaceId { get; init; }
    public string? WorkItemId { get; init; }
    public string? FormId { get; init; }
    public string? FormName { get; init; }
    public string? DefaultTypeId { get; init; }
    public string? InitialStateId { get; init; }
    public JsonElement? Layout { get; init; }
    public RuntimePermissionsDto Permissions { get; init; } = new();
    public IReadOnlyList<WorkItemTypeOptionDto> Types { get; init; } = Array.Empty<WorkItemTypeOptionDto>();
    public IReadOnlyDictionary<string, FormFieldRuntimeDto> Fields { get; init; }
        = new Dictionary<string, FormFieldRuntimeDto>();
    public IReadOnlyDictionary<string, FieldBehaviorDto> FieldBehaviors { get; init; }
        = new Dictionary<string, FieldBehaviorDto>();

    /// <summary>Workspace pool alan tanımları (op_fields) — UI'nın ayrı DG listesi çekmesini önler.</summary>
    public IReadOnlyList<Dictionary<string, object?>> PoolFields { get; init; }
        = Array.Empty<Dictionary<string, object?>>();
}

public sealed class WorkItemTypeOptionDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Category { get; init; }
}

public sealed class FormFieldRuntimeDto
{
    public required string Key { get; init; }
    public string? Label { get; init; }
    public string? FieldType { get; init; }
    public object? Value { get; init; }
}

public sealed class FieldBehaviorDto
{
    public bool Visible { get; init; } = true;
    public bool Readonly { get; init; }
    public bool Required { get; init; }
    public bool Masked { get; init; }
}
