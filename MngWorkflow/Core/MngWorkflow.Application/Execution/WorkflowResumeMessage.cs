namespace MngWorkflow.Application.Execution;

public sealed class WorkflowResumeMessage
{
    public required string InstanceId { get; init; }
    public required string WorkflowVersionId { get; init; }
    public required string NodeId { get; init; }
    public string EdgeKey { get; init; } = "default";
    public required string CorrelationId { get; init; }
    public required string DomainId { get; init; }
    public required string DomainName { get; init; }
}
