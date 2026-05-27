namespace MngOperations.Application.Rules;

public sealed record RuleExecutionContext
{
    public required string WorkspaceId { get; init; }
    public required string Trigger { get; init; }
    public required IReadOnlyDictionary<string, object?> WorkItem { get; init; }
    public string? TypeId { get; init; }
    public string? BoardId { get; init; }
    public string? TransitionKey { get; init; }
    public string? FromStateId { get; init; }
    public string? ToStateId { get; init; }
    public string? StateId { get; init; }
    public string? WorkItemId { get; init; }
    public string? WorkItemKey { get; init; }
}

public sealed class RuleValidationError
{
    public required string RuleId { get; init; }
    public string? RuleName { get; init; }
    public required string Message { get; init; }
    public string? MessageTr { get; init; }
}

public sealed class RuleExecutionResult
{
    public IReadOnlyDictionary<string, object?> FieldMutations { get; init; }
        = new Dictionary<string, object?>();

    public IReadOnlyList<RuleValidationError> ValidationErrors { get; init; }
        = Array.Empty<RuleValidationError>();

    public IReadOnlyList<RuleSideEffect> SideEffects { get; init; }
        = Array.Empty<RuleSideEffect>();

    public bool HasValidationErrors => ValidationErrors.Count > 0;
}

public sealed class RuleSideEffect
{
    public required string Type { get; init; }
    public IReadOnlyDictionary<string, object?> Payload { get; init; }
        = new Dictionary<string, object?>();
}
