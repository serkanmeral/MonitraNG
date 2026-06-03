using MngWorkflow.Application.Contracts;
using MngWorkflow.Application.Repositories;
using MngWorkflow.Application.Services;
using MngWorkflow.Domain.Entities;

namespace MngWorkflow.Infrastructure.Services;

public sealed class WorkflowDefinitionService : IWorkflowDefinitionService
{
    private readonly IWorkflowDomainAccessor _domain;
    private readonly IWorkflowDefinitionRepository _definitions;

    public WorkflowDefinitionService(IWorkflowDomainAccessor domain, IWorkflowDefinitionRepository definitions)
    {
        _domain = domain;
        _definitions = definitions;
    }

    public async Task<WorkflowDefinitionDocument> CreateAsync(CreateWorkflowDefinitionRequest request, CancellationToken cancellationToken = default)
    {
        var ctx = _domain.GetRequiredDomain();
        if (string.IsNullOrWhiteSpace(request.Key))
            throw new ArgumentException("key is required.");
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("name is required.");

        var key = request.Key.Trim();
        if (await _definitions.GetByKeyAsync(ctx.DomainName, key, cancellationToken) != null)
            throw new InvalidOperationException($"Workflow key '{key}' already exists.");

        var now = DateTime.UtcNow;
        var doc = new WorkflowDefinitionDocument
        {
            DomainId = ctx.DomainId,
            DomainName = ctx.DomainName,
            Key = key,
            Name = request.Name.Trim(),
            Category = request.Category?.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        await _definitions.InsertAsync(doc, cancellationToken);
        return doc;
    }

    public Task<WorkflowDefinitionDocument?> GetAsync(string workflowId, CancellationToken cancellationToken = default)
    {
        var ctx = _domain.GetRequiredDomain();
        return _definitions.GetByIdAsync(ctx.DomainName, workflowId, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowDefinitionSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        var ctx = _domain.GetRequiredDomain();
        var items = await _definitions.ListAsync(ctx.DomainName, cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<WorkflowDefinitionDocument?> UpdateAsync(string workflowId, UpdateWorkflowDefinitionRequest request, CancellationToken cancellationToken = default)
    {
        var ctx = _domain.GetRequiredDomain();
        var doc = await _definitions.GetByIdAsync(ctx.DomainName, workflowId, cancellationToken);
        if (doc == null)
            return null;

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("name is required.");

        doc.Name = request.Name.Trim();
        doc.Category = request.Category?.Trim();
        doc.UpdatedAt = DateTime.UtcNow;
        await _definitions.UpdateAsync(doc, cancellationToken);
        return doc;
    }

    private static WorkflowDefinitionSummary Map(WorkflowDefinitionDocument doc) =>
        new()
        {
            Id = doc.Id,
            Key = doc.Key,
            Name = doc.Name,
            Category = doc.Category,
            CurrentVersion = doc.CurrentVersion,
            CurrentVersionId = doc.CurrentVersionId,
            CreatedAt = doc.CreatedAt,
            UpdatedAt = doc.UpdatedAt
        };
}
