using MngWorkflow.Application.Contracts;
using MngWorkflow.Application.Execution;
using MngWorkflow.Application.Repositories;
using MngWorkflow.Application.Services;
using MngWorkflow.Application.Smoke;
using MngWorkflow.Domain.Entities;
using MngWorkflow.Domain.Enums;
using MngWorkflow.Infrastructure.Utilities;

namespace MngWorkflow.Infrastructure.Services;

public sealed class WorkflowRunService : IWorkflowRunService
{
    private readonly IWorkflowDomainAccessor _domain;
    private readonly IWorkflowDefinitionRepository _definitions;
    private readonly IWorkflowVersionRepository _versions;
    private readonly IWorkflowInstanceRepository _instances;
    private readonly INodeExecutionRepository _executions;
    private readonly IWorkflowQueuePublisher _publisher;

    public WorkflowRunService(
        IWorkflowDomainAccessor domain,
        IWorkflowDefinitionRepository definitions,
        IWorkflowVersionRepository versions,
        IWorkflowInstanceRepository instances,
        INodeExecutionRepository executions,
        IWorkflowQueuePublisher publisher)
    {
        _domain = domain;
        _definitions = definitions;
        _versions = versions;
        _instances = instances;
        _executions = executions;
        _publisher = publisher;
    }

    public async Task<WorkflowRunResult> StartSmokeRunAsync(
        string domainName,
        string domainId,
        int eventValue,
        CancellationToken cancellationToken = default)
    {
        var version = SmokeWorkflowDefinition.Create(domainId, domainName);
        await _versions.UpsertAsync(version, cancellationToken);

        return await StartFromVersionAsync(
            version,
            "manual",
            new Dictionary<string, object?> { ["value"] = eventValue },
            cancellationToken);
    }

    public async Task<WorkflowRunResult> StartRunAsync(StartWorkflowRunRequest request, CancellationToken cancellationToken = default)
    {
        var ctx = _domain.GetRequiredDomain();
        var version = await ResolveVersionAsync(ctx, request, cancellationToken)
            ?? throw new InvalidOperationException("Published workflow version not found.");

        if (version.Status != WorkflowVersionStatus.Published)
            throw new InvalidOperationException("Only published versions can be executed.");

        var triggerData = request.TriggerData != null
            ? WorkflowJsonNormalizer.NormalizeDictionary(request.TriggerData)
            : new Dictionary<string, object?>();
        return await StartFromVersionAsync(version, request.TriggerType, triggerData, cancellationToken);
    }

    public Task<WorkflowRunResult> StartEventRunAsync(
        WorkflowVersionDocument version,
        Dictionary<string, object?> eventPayload,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        var payload = WorkflowJsonNormalizer.NormalizeDictionary(eventPayload);
        return StartFromVersionAsync(version, "event", payload, cancellationToken, correlationId);
    }

    public async Task<WorkflowRunDetail?> GetRunDetailAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        var ctx = _domain.GetRequiredDomain();
        var instance = await _instances.GetByIdAsync(ctx.DomainName, instanceId, cancellationToken);
        if (instance == null)
            return null;

        var executions = await _executions.ListByInstanceAsync(ctx.DomainName, instanceId, cancellationToken);
        return new WorkflowRunDetail
        {
            Instance = MapInstance(instance),
            Executions = executions.Select(MapExecution).ToList()
        };
    }

    public async Task<IReadOnlyList<WorkflowInstanceSummary>> ListRunsAsync(WorkflowRunHistoryQuery query, CancellationToken cancellationToken = default)
    {
        var ctx = _domain.GetRequiredDomain();
        var items = await _instances.ListAsync(
            ctx.DomainName,
            query.WorkflowId,
            query.Status,
            query.Skip,
            query.Limit,
            cancellationToken);

        return items.Select(MapInstance).ToList();
    }

    private async Task<WorkflowVersionDocument?> ResolveVersionAsync(
        WorkflowDomainContext ctx,
        StartWorkflowRunRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.WorkflowVersionId))
            return await _versions.GetByIdAsync(ctx.DomainName, request.WorkflowVersionId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.WorkflowId))
        {
            var definition = await _definitions.GetByIdAsync(ctx.DomainName, request.WorkflowId!, cancellationToken);
            if (definition?.CurrentVersionId != null)
                return await _versions.GetByIdAsync(ctx.DomainName, definition.CurrentVersionId, cancellationToken);

            return await _versions.GetPublishedByWorkflowIdAsync(ctx.DomainName, request.WorkflowId!, cancellationToken);
        }

        return null;
    }

    private async Task<WorkflowRunResult> StartFromVersionAsync(
        WorkflowVersionDocument version,
        string triggerType,
        Dictionary<string, object?> triggerData,
        CancellationToken cancellationToken,
        string? correlationId = null)
    {
        var instance = new WorkflowInstanceDocument
        {
            WorkflowId = version.WorkflowId,
            WorkflowVersionId = version.Id,
            DomainId = version.DomainId,
            DomainName = version.DomainName,
            Status = WorkflowInstanceStatus.Running,
            CurrentNodes = [version.EntryNodeId],
            TriggerType = triggerType,
            TriggerData = triggerData,
            CorrelationId = string.IsNullOrWhiteSpace(correlationId) ? Guid.NewGuid().ToString("N") : correlationId,
            ExecutionContext = new Dictionary<string, object?>
            {
                ["event"] = triggerData,
                ["variables"] = new Dictionary<string, object?>(),
                ["outputs"] = new Dictionary<string, object?>()
            },
            Revision = 0
        };

        await _instances.InsertAsync(instance, cancellationToken);

        await _publisher.PublishExecutionAsync(new WorkflowExecutionMessage
        {
            InstanceId = instance.Id,
            WorkflowVersionId = version.Id,
            NodeId = version.EntryNodeId,
            Attempt = 1,
            CorrelationId = instance.CorrelationId,
            DomainId = version.DomainId,
            DomainName = version.DomainName
        }, cancellationToken);

        return new WorkflowRunResult(instance.Id, instance.CorrelationId, version.Id, version.EntryNodeId);
    }

    private static WorkflowInstanceSummary MapInstance(WorkflowInstanceDocument doc) =>
        new()
        {
            Id = doc.Id,
            WorkflowId = doc.WorkflowId,
            WorkflowVersionId = doc.WorkflowVersionId,
            Status = doc.Status,
            CorrelationId = doc.CorrelationId,
            TriggerType = doc.TriggerType,
            StartedAt = doc.StartedAt,
            FinishedAt = doc.FinishedAt
        };

    private static NodeExecutionSummary MapExecution(NodeExecutionDocument doc) =>
        new()
        {
            NodeId = doc.NodeId,
            Attempt = doc.Attempt,
            Status = doc.Status,
            ErrorMessage = doc.ErrorMessage,
            StartedAt = doc.StartedAt,
            FinishedAt = doc.FinishedAt
        };
}
