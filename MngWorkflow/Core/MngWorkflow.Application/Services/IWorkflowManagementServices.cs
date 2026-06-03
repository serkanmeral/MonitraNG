using MngWorkflow.Application.Contracts;
using MngWorkflow.Application.Execution;
using MngWorkflow.Domain.Entities;
using MngWorkflow.Domain.Enums;

namespace MngWorkflow.Application.Services;

public interface IWorkflowQueuePublisher
{
    Task PublishExecutionAsync(WorkflowExecutionMessage message, CancellationToken cancellationToken = default);

    Task PublishRetryAsync(WorkflowExecutionMessage message, int failedAttempt, CancellationToken cancellationToken = default);

    Task PublishDeadLetterAsync(WorkflowDeadLetterMessage message, CancellationToken cancellationToken = default);

    Task PublishDelayResumeAsync(WorkflowResumeMessage message, int delaySeconds, CancellationToken cancellationToken = default);
}

public interface IWorkflowExecutionEngine
{
    Task ProcessMessageAsync(WorkflowExecutionMessage message, CancellationToken cancellationToken = default);
}

public interface IWorkflowDomainAccessor
{
    WorkflowDomainContext GetRequiredDomain();
}

public interface IWorkflowDefinitionService
{
    Task<WorkflowDefinitionDocument> CreateAsync(CreateWorkflowDefinitionRequest request, CancellationToken cancellationToken = default);
    Task<WorkflowDefinitionDocument?> GetAsync(string workflowId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowDefinitionSummary>> ListAsync(CancellationToken cancellationToken = default);
    Task<WorkflowDefinitionDocument?> UpdateAsync(string workflowId, UpdateWorkflowDefinitionRequest request, CancellationToken cancellationToken = default);
}

public interface IWorkflowVersionService
{
    Task<WorkflowVersionDocument> CreateDraftAsync(string workflowId, CreateWorkflowVersionRequest request, CancellationToken cancellationToken = default);
    Task<WorkflowVersionDocument?> GetAsync(string versionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowVersionDocument>> ListByWorkflowAsync(string workflowId, CancellationToken cancellationToken = default);
    Task<WorkflowVersionDocument?> UpdateDraftAsync(string versionId, UpdateWorkflowVersionRequest request, CancellationToken cancellationToken = default);
    Task<WorkflowVersionDocument> PublishAsync(string versionId, CancellationToken cancellationToken = default);
}

public interface IWorkflowRunService
{
    Task<WorkflowRunResult> StartSmokeRunAsync(string domainName, string domainId, int eventValue, CancellationToken cancellationToken = default);
    Task<WorkflowRunResult> StartRunAsync(StartWorkflowRunRequest request, CancellationToken cancellationToken = default);

    Task<WorkflowRunResult> StartEventRunAsync(
        WorkflowVersionDocument version,
        Dictionary<string, object?> eventPayload,
        string? correlationId,
        CancellationToken cancellationToken = default);

    Task<WorkflowRunDetail?> GetRunDetailAsync(string instanceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowInstanceSummary>> ListRunsAsync(WorkflowRunHistoryQuery query, CancellationToken cancellationToken = default);
}

public interface IWorkflowApprovalService
{
    Task<IReadOnlyList<WorkflowApprovalSummary>> ListAsync(WorkflowApprovalStatus? status, int skip, int limit, CancellationToken cancellationToken = default);
    Task<WorkflowApprovalSummary?> GetAsync(string approvalId, CancellationToken cancellationToken = default);
    Task<WorkflowApprovalDecisionResult> DecideAsync(string approvalId, DecideWorkflowApprovalRequest request, CancellationToken cancellationToken = default);
}

public interface IWorkflowSecretService
{
    Task<WorkflowSecretSummary> UpsertAsync(CreateWorkflowSecretRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowSecretSummary>> ListAsync(CancellationToken cancellationToken = default);
}

public interface IWorkflowSecretResolver
{
    string Resolve(string domainName, string template);
}

public interface IWorkflowResumeService
{
    Task<WorkflowResumeResult> ResumeFromWaitingNodeAsync(
        string domainName,
        string instanceId,
        string nodeId,
        string edgeKey,
        Dictionary<string, object?>? additionalOutput = null,
        CancellationToken cancellationToken = default);
}

public sealed record WorkflowResumeResult(
    string InstanceId,
    string NodeId,
    string EdgeKey,
    WorkflowInstanceStatus InstanceStatus);

public interface IWorkflowScheduleSyncService
{
    Task SyncPublishedVersionAsync(WorkflowVersionDocument version, CancellationToken cancellationToken = default);
    Task RemoveForWorkflowAsync(string domainName, string workflowId, CancellationToken cancellationToken = default);
}

public interface IWorkflowHookService
{
    Task<WorkflowResumeResult> ResumeDelayAsync(WorkflowDelayResumeRequest request, CancellationToken cancellationToken = default);
    Task<WorkflowRunResult> RunScheduleTriggerAsync(WorkflowScheduleRunRequest request, CancellationToken cancellationToken = default);
}

public sealed record WorkflowApprovalDecisionResult(
    string ApprovalId,
    string InstanceId,
    bool Approved,
    string EdgeKey,
    WorkflowInstanceStatus InstanceStatus);

public sealed record WorkflowRunResult(
    string InstanceId,
    string CorrelationId,
    string WorkflowVersionId,
    string EntryNodeId);
