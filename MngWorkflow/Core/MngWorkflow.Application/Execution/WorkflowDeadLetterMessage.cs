namespace MngWorkflow.Application.Execution;

public sealed class WorkflowDeadLetterMessage
{
    public required WorkflowExecutionMessage Execution { get; init; }
    public required string Reason { get; init; }
    public DateTime FailedAt { get; init; } = DateTime.UtcNow;
}
