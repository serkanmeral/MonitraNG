using MngWorkflow.Domain.Enums;

namespace MngWorkflow.Application.Contracts;

public sealed class DecideWorkflowApprovalRequest
{
    public bool Approved { get; set; }
    public string? Comment { get; set; }
    public string? DecidedBy { get; set; }
}

public sealed class WorkflowApprovalSummary
{
    public required string Id { get; init; }
    public required string InstanceId { get; init; }
    public required string WorkflowId { get; init; }
    public required string NodeId { get; init; }
    public required string ApproverTarget { get; init; }
    public required WorkflowApprovalStatus Status { get; init; }
    public string? DecidedBy { get; init; }
    public string? Comment { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? DecidedAt { get; init; }
}

public sealed class CreateWorkflowSecretRequest
{
    public required string Key { get; set; }
    public required string Value { get; set; }
}

public sealed class WorkflowSecretSummary
{
    public required string Id { get; init; }
    public required string Key { get; init; }
    public DateTime UpdatedAt { get; init; }
}
