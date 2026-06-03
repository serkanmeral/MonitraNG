namespace MngWorkflow.Application.Execution;

public sealed class WorkflowExecutionMessage
{
    public required string InstanceId { get; init; }
    public required string WorkflowVersionId { get; init; }
    public required string NodeId { get; init; }
    public int Attempt { get; init; } = 1;
    public required string CorrelationId { get; init; }
    public required string DomainId { get; init; }
    public required string DomainName { get; init; }
}
