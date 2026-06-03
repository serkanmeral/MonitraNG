namespace MngOperations.Application.Contracts.Workflow;

public sealed class StartWorkflowRunRequest
{
    public string? WorkflowId { get; init; }
    public string? WorkflowVersionId { get; init; }
    public string TriggerType { get; init; } = "op_rules";
    public Dictionary<string, object?>? TriggerData { get; init; }
}

public sealed class StartWorkflowRunResponse
{
    public required string InstanceId { get; init; }
    public required string CorrelationId { get; init; }
    public string? WorkflowVersionId { get; init; }
    public string? EntryNodeId { get; init; }
    public string? Status { get; init; }
}
