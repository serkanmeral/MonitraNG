using MngWorkflow.Domain.Entities;
using MngWorkflow.Domain.Enums;

namespace MngWorkflow.Application.Repositories;

public interface IWorkflowDefinitionRepository
{
    Task InsertAsync(WorkflowDefinitionDocument definition, CancellationToken cancellationToken = default);
    Task<WorkflowDefinitionDocument?> GetByIdAsync(string domainName, string workflowId, CancellationToken cancellationToken = default);
    Task<WorkflowDefinitionDocument?> GetByKeyAsync(string domainName, string key, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowDefinitionDocument>> ListAsync(string domainName, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(WorkflowDefinitionDocument definition, CancellationToken cancellationToken = default);
}

public interface IWorkflowVersionRepository
{
    Task InsertAsync(WorkflowVersionDocument version, CancellationToken cancellationToken = default);
    Task UpsertAsync(WorkflowVersionDocument version, CancellationToken cancellationToken = default);
    Task<WorkflowVersionDocument?> GetByIdAsync(string domainName, string versionId, CancellationToken cancellationToken = default);
    Task<WorkflowVersionDocument?> GetPublishedByWorkflowIdAsync(string domainName, string workflowId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowVersionDocument>> ListByWorkflowIdAsync(string domainName, string workflowId, CancellationToken cancellationToken = default);
    Task<int> GetMaxVersionNumberAsync(string domainName, string workflowId, CancellationToken cancellationToken = default);
    Task<bool> ReplaceAsync(WorkflowVersionDocument version, CancellationToken cancellationToken = default);
    Task ArchivePublishedExceptAsync(string domainName, string workflowId, string exceptVersionId, CancellationToken cancellationToken = default);
}

public interface IWorkflowInstanceRepository
{
    Task InsertAsync(WorkflowInstanceDocument instance, CancellationToken cancellationToken = default);
    Task<WorkflowInstanceDocument?> GetByIdAsync(string domainName, string instanceId, CancellationToken cancellationToken = default);
    Task<bool> TryUpdateAsync(WorkflowInstanceDocument instance, long expectedRevision, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowInstanceDocument>> ListAsync(
        string domainName,
        string? workflowId,
        WorkflowInstanceStatus? status,
        int skip,
        int limit,
        CancellationToken cancellationToken = default);
}

public interface INodeExecutionRepository
{
    Task<bool> IsSuccessfulAsync(string domainName, string instanceId, string nodeId, int attempt, CancellationToken cancellationToken = default);
    Task InsertAsync(NodeExecutionDocument execution, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NodeExecutionDocument>> ListByInstanceAsync(string domainName, string instanceId, CancellationToken cancellationToken = default);
}

public interface IWorkflowTriggerRepository
{
    Task ReplaceForWorkflowAsync(string domainName, string workflowId, IReadOnlyList<WorkflowTriggerProjectionDocument> triggers, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowTriggerProjectionDocument>> FindByEventTypeAsync(string domainName, string eventType, CancellationToken cancellationToken = default);
}

public interface IWorkflowApprovalRepository
{
    Task InsertAsync(WorkflowApprovalDocument approval, CancellationToken cancellationToken = default);
    Task<WorkflowApprovalDocument?> GetByIdAsync(string domainName, string approvalId, CancellationToken cancellationToken = default);
    Task<WorkflowApprovalDocument?> GetPendingByInstanceNodeAsync(string domainName, string instanceId, string nodeId, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(WorkflowApprovalDocument approval, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowApprovalDocument>> ListAsync(string domainName, WorkflowApprovalStatus? status, int skip, int limit, CancellationToken cancellationToken = default);
}

public interface IWorkflowSecretRepository
{
    Task InsertAsync(WorkflowSecretDocument secret, CancellationToken cancellationToken = default);
    Task<WorkflowSecretDocument?> GetByKeyAsync(string domainName, string key, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowSecretDocument>> ListAsync(string domainName, CancellationToken cancellationToken = default);
    Task<bool> ReplaceAsync(WorkflowSecretDocument secret, CancellationToken cancellationToken = default);
}
