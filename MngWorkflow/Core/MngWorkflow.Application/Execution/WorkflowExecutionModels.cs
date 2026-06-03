using MngWorkflow.Domain.Entities;

namespace MngWorkflow.Application.Execution;

public sealed class WorkflowExecutionContext
{
    public required string InstanceId { get; init; }
    public required string WorkflowVersionId { get; init; }
    public required string DomainId { get; init; }
    public required string DomainName { get; init; }
    public required string CorrelationId { get; init; }
    public required Dictionary<string, object?> Event { get; init; }
    public required Dictionary<string, object?> Variables { get; init; }
    public required Dictionary<string, object?> Outputs { get; init; }
}

public sealed class NodeExecutionResult
{
    public bool Success { get; init; }
    public List<string> NextEdges { get; init; } = ["default"];
    public Dictionary<string, object?> Output { get; init; } = new();
    public string? ErrorMessage { get; init; }
    public bool ShouldWait { get; init; }
    public string? WaitingType { get; init; }

    /// <summary>False ise retry/DLQ atlanır, instance doğrudan Failed olur.</summary>
    public bool Retryable { get; init; } = true;

    public static NodeExecutionResult Ok(IReadOnlyList<string>? nextEdges = null, Dictionary<string, object?>? output = null) =>
        new()
        {
            Success = true,
            NextEdges = nextEdges?.ToList() ?? ["default"],
            Output = output ?? new Dictionary<string, object?>()
        };

    public static NodeExecutionResult Fail(string message, bool retryable = true) =>
        new() { Success = false, ErrorMessage = message, Retryable = retryable };

    public static NodeExecutionResult Wait(string waitingType, Dictionary<string, object?>? output = null) =>
        new()
        {
            Success = true,
            ShouldWait = true,
            WaitingType = waitingType,
            Output = output ?? new Dictionary<string, object?>()
        };
}
