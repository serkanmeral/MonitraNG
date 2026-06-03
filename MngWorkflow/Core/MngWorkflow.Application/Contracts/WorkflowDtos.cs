using MngWorkflow.Domain.Entities;
using MngWorkflow.Domain.Enums;

namespace MngWorkflow.Application.Contracts;

public sealed record WorkflowDomainContext(string DomainId, string DomainName);

public sealed class CreateWorkflowDefinitionRequest
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
}

public sealed class UpdateWorkflowDefinitionRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
}

public sealed class CreateWorkflowVersionRequest
{
    public string EntryNodeId { get; set; } = string.Empty;
    public List<WorkflowNodeDefinition> Nodes { get; set; } = new();
    public List<WorkflowEdgeDefinition> Edges { get; set; } = new();
    public List<WorkflowTriggerDefinition> Triggers { get; set; } = new();
}

public sealed class UpdateWorkflowVersionRequest
{
    public string EntryNodeId { get; set; } = string.Empty;
    public List<WorkflowNodeDefinition> Nodes { get; set; } = new();
    public List<WorkflowEdgeDefinition> Edges { get; set; } = new();
    public List<WorkflowTriggerDefinition> Triggers { get; set; } = new();
}

public sealed class StartWorkflowRunRequest
{
    public string? WorkflowId { get; set; }
    public string? WorkflowVersionId { get; set; }
    public string TriggerType { get; set; } = "manual";
    public Dictionary<string, object?>? TriggerData { get; set; }
}

public sealed class WorkflowRunHistoryQuery
{
    public string? WorkflowId { get; set; }
    public WorkflowInstanceStatus? Status { get; set; }
    public int Skip { get; set; }
    public int Limit { get; set; } = 50;
}

public sealed class WorkflowDefinitionSummary
{
    public required string Id { get; init; }
    public required string Key { get; init; }
    public required string Name { get; init; }
    public string? Category { get; init; }
    public int CurrentVersion { get; init; }
    public string? CurrentVersionId { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class WorkflowInstanceSummary
{
    public required string Id { get; init; }
    public required string WorkflowId { get; init; }
    public required string WorkflowVersionId { get; init; }
    public required WorkflowInstanceStatus Status { get; init; }
    public required string CorrelationId { get; init; }
    public required string TriggerType { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? FinishedAt { get; init; }
}

public sealed class WorkflowRunDetail
{
    public required WorkflowInstanceSummary Instance { get; init; }
    public IReadOnlyList<NodeExecutionSummary> Executions { get; init; } = Array.Empty<NodeExecutionSummary>();
}

public sealed class NodeExecutionSummary
{
    public required string NodeId { get; init; }
    public int Attempt { get; init; }
    public required NodeExecutionStatus Status { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? FinishedAt { get; init; }
}
