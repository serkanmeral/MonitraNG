using MngWorkflow.Application.Contracts;
using MngWorkflow.Application.Repositories;
using MngWorkflow.Application.Services;
using MngWorkflow.Domain.Constants;
using MngWorkflow.Domain.Entities;
using MngWorkflow.Domain.Enums;
using MngWorkflow.Infrastructure.Utilities;
using MngWorkflow.Infrastructure.Validation;

namespace MngWorkflow.Infrastructure.Services;

public sealed class WorkflowVersionService : IWorkflowVersionService
{
    private readonly IWorkflowDomainAccessor _domain;
    private readonly IWorkflowDefinitionRepository _definitions;
    private readonly IWorkflowVersionRepository _versions;
    private readonly IWorkflowTriggerSyncService _triggerSync;
    private readonly IWorkflowScheduleSyncService _scheduleSync;

    public WorkflowVersionService(
        IWorkflowDomainAccessor domain,
        IWorkflowDefinitionRepository definitions,
        IWorkflowVersionRepository versions,
        IWorkflowTriggerSyncService triggerSync,
        IWorkflowScheduleSyncService scheduleSync)
    {
        _domain = domain;
        _definitions = definitions;
        _versions = versions;
        _triggerSync = triggerSync;
        _scheduleSync = scheduleSync;
    }

    public async Task<WorkflowVersionDocument> CreateDraftAsync(string workflowId, CreateWorkflowVersionRequest request, CancellationToken cancellationToken = default)
    {
        var ctx = _domain.GetRequiredDomain();
        var definition = await _definitions.GetByIdAsync(ctx.DomainName, workflowId, cancellationToken)
            ?? throw new KeyNotFoundException($"Workflow '{workflowId}' not found.");

        WorkflowGraphValidator.Validate(request.EntryNodeId, request.Nodes, request.Edges);

        var maxVersion = await _versions.GetMaxVersionNumberAsync(ctx.DomainName, workflowId, cancellationToken);
        var now = DateTime.UtcNow;

        var version = new WorkflowVersionDocument
        {
            WorkflowId = workflowId,
            DomainId = ctx.DomainId,
            DomainName = ctx.DomainName,
            Version = maxVersion + 1,
            Status = WorkflowVersionStatus.Draft,
            EntryNodeId = request.EntryNodeId,
            Nodes = WorkflowJsonNormalizer.NormalizeNodes(request.Nodes),
            Edges = request.Edges,
            Triggers = NormalizeTriggers(request.Triggers),
            CreatedAt = now,
            UpdatedAt = now
        };

        await _versions.InsertAsync(version, cancellationToken);
        definition.UpdatedAt = now;
        await _definitions.UpdateAsync(definition, cancellationToken);
        return version;
    }

    public Task<WorkflowVersionDocument?> GetAsync(string versionId, CancellationToken cancellationToken = default)
    {
        var ctx = _domain.GetRequiredDomain();
        return _versions.GetByIdAsync(ctx.DomainName, versionId, cancellationToken);
    }

    public Task<IReadOnlyList<WorkflowVersionDocument>> ListByWorkflowAsync(string workflowId, CancellationToken cancellationToken = default)
    {
        var ctx = _domain.GetRequiredDomain();
        return _versions.ListByWorkflowIdAsync(ctx.DomainName, workflowId, cancellationToken);
    }

    public async Task<WorkflowVersionDocument?> UpdateDraftAsync(string versionId, UpdateWorkflowVersionRequest request, CancellationToken cancellationToken = default)
    {
        var ctx = _domain.GetRequiredDomain();
        var version = await _versions.GetByIdAsync(ctx.DomainName, versionId, cancellationToken);
        if (version == null)
            return null;

        if (version.Status != WorkflowVersionStatus.Draft)
            throw new InvalidOperationException("Only draft versions can be updated.");

        WorkflowGraphValidator.Validate(request.EntryNodeId, request.Nodes, request.Edges);

        version.EntryNodeId = request.EntryNodeId;
        version.Nodes = WorkflowJsonNormalizer.NormalizeNodes(request.Nodes);
        version.Edges = request.Edges;
        version.Triggers = NormalizeTriggers(request.Triggers);
        version.UpdatedAt = DateTime.UtcNow;

        if (!await _versions.ReplaceAsync(version, cancellationToken))
            throw new InvalidOperationException("Version update failed.");

        return version;
    }

    public async Task<WorkflowVersionDocument> PublishAsync(string versionId, CancellationToken cancellationToken = default)
    {
        var ctx = _domain.GetRequiredDomain();
        var version = await _versions.GetByIdAsync(ctx.DomainName, versionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Version '{versionId}' not found.");

        if (version.Status != WorkflowVersionStatus.Draft)
            throw new InvalidOperationException("Only draft versions can be published.");

        WorkflowGraphValidator.Validate(version.EntryNodeId, version.Nodes, version.Edges);

        await _versions.ArchivePublishedExceptAsync(ctx.DomainName, version.WorkflowId, version.Id, cancellationToken);

        var now = DateTime.UtcNow;
        version.Status = WorkflowVersionStatus.Published;
        version.PublishedAt = now;
        version.UpdatedAt = now;
        await _versions.UpsertAsync(version, cancellationToken);

        var definition = await _definitions.GetByIdAsync(ctx.DomainName, version.WorkflowId, cancellationToken)
            ?? throw new InvalidOperationException("Workflow definition missing for version.");

        definition.CurrentVersion = version.Version;
        definition.CurrentVersionId = version.Id;
        definition.UpdatedAt = now;
        await _definitions.UpdateAsync(definition, cancellationToken);

        await _triggerSync.SyncPublishedVersionAsync(version, cancellationToken);
        await _scheduleSync.SyncPublishedVersionAsync(version, cancellationToken);

        return version;
    }

    private static List<WorkflowTriggerDefinition> NormalizeTriggers(IReadOnlyList<WorkflowTriggerDefinition> triggers) =>
        triggers.Select(t => new WorkflowTriggerDefinition
        {
            Type = string.IsNullOrWhiteSpace(t.Type) ? WorkflowTriggerTypes.Event : t.Type,
            Config = WorkflowJsonNormalizer.NormalizeDictionary(t.Config),
            FilterExpression = t.FilterExpression,
            Enabled = t.Enabled
        }).ToList();
}
