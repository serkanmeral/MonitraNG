namespace MngOperations.Application.Contracts.Automations;

public sealed class SimulateWorkspaceAutomationRequest
{
    public required string WorkItemId { get; init; }

    /// <summary>false = yalnızca önizleme; true = aksiyonu gerçekten çalıştırır.</summary>
    public bool Execute { get; init; }
}

public sealed class WorkspaceAutomationSimulatePreviewDto
{
    public string? ResolvedTitle { get; init; }
    public string? ResolvedDescription { get; init; }
    public string? TargetBoardId { get; init; }
    public string? TargetTypeId { get; init; }
    public string? ResolvedAssignee { get; init; }
    public IReadOnlyDictionary<string, object?> ResolvedFields { get; init; }
        = new Dictionary<string, object?>();
}

public sealed class WorkspaceAutomationSimulateCreatedDto
{
    public required string Id { get; init; }
    public required string Key { get; init; }
    public string? Code { get; init; }
}

public sealed class SimulateWorkspaceAutomationResult
{
    public bool Matched { get; init; }
    public string? Reason { get; init; }
    public bool Executed { get; init; }
    public WorkspaceAutomationSimulatePreviewDto? Preview { get; init; }
    public WorkspaceAutomationSimulateCreatedDto? CreatedWorkItem { get; init; }
}
