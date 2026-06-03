using MongoDB.Driver;
using MngWorkflow.Application.Repositories;
using MngWorkflow.Domain.Constants;
using MngWorkflow.Domain.Entities;
using MngWorkflow.Domain.Enums;

namespace MngWorkflow.Infrastructure.Persistence.Repositories;

public sealed class WorkflowDefinitionRepository : IWorkflowDefinitionRepository
{
    private readonly IWorkflowMongoContext _context;
    private readonly WorkflowIndexInitializer _indexInitializer;

    public WorkflowDefinitionRepository(IWorkflowMongoContext context, WorkflowIndexInitializer indexInitializer)
    {
        _context = context;
        _indexInitializer = indexInitializer;
    }

    public async Task InsertAsync(WorkflowDefinitionDocument definition, CancellationToken cancellationToken = default)
    {
        await _indexInitializer.EnsureAsync(definition.DomainName, cancellationToken);
        var col = Collection(definition.DomainName);
        await col.InsertOneAsync(definition, cancellationToken: cancellationToken);
    }

    public async Task<WorkflowDefinitionDocument?> GetByIdAsync(string domainName, string workflowId, CancellationToken cancellationToken = default)
    {
        await _indexInitializer.EnsureAsync(domainName, cancellationToken);
        return await Collection(domainName).Find(x => x.Id == workflowId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<WorkflowDefinitionDocument?> GetByKeyAsync(string domainName, string key, CancellationToken cancellationToken = default)
    {
        await _indexInitializer.EnsureAsync(domainName, cancellationToken);
        return await Collection(domainName).Find(x => x.Key == key).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowDefinitionDocument>> ListAsync(string domainName, CancellationToken cancellationToken = default)
    {
        await _indexInitializer.EnsureAsync(domainName, cancellationToken);
        return await Collection(domainName)
            .Find(FilterDefinition<WorkflowDefinitionDocument>.Empty)
            .SortByDescending(x => x.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> UpdateAsync(WorkflowDefinitionDocument definition, CancellationToken cancellationToken = default)
    {
        var result = await Collection(definition.DomainName).ReplaceOneAsync(
            x => x.Id == definition.Id,
            definition,
            cancellationToken: cancellationToken);
        return result.IsAcknowledged && result.ModifiedCount == 1;
    }

    private IMongoCollection<WorkflowDefinitionDocument> Collection(string domainName) =>
        _context.GetDatabase(domainName).GetCollection<WorkflowDefinitionDocument>(WorkflowCollectionNames.Definitions);
}

public sealed class WorkflowVersionRepository : IWorkflowVersionRepository
{
    private readonly IWorkflowMongoContext _context;
    private readonly WorkflowIndexInitializer _indexInitializer;

    public WorkflowVersionRepository(IWorkflowMongoContext context, WorkflowIndexInitializer indexInitializer)
    {
        _context = context;
        _indexInitializer = indexInitializer;
    }

    public async Task InsertAsync(WorkflowVersionDocument version, CancellationToken cancellationToken = default)
    {
        await _indexInitializer.EnsureAsync(version.DomainName, cancellationToken);
        await Collection(version.DomainName).InsertOneAsync(version, cancellationToken: cancellationToken);
    }

    public async Task UpsertAsync(WorkflowVersionDocument version, CancellationToken cancellationToken = default)
    {
        await _indexInitializer.EnsureAsync(version.DomainName, cancellationToken);
        await Collection(version.DomainName).ReplaceOneAsync(
            x => x.Id == version.Id,
            version,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task<WorkflowVersionDocument?> GetByIdAsync(string domainName, string versionId, CancellationToken cancellationToken = default)
    {
        await _indexInitializer.EnsureAsync(domainName, cancellationToken);
        return await Collection(domainName).Find(x => x.Id == versionId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<WorkflowVersionDocument?> GetPublishedByWorkflowIdAsync(string domainName, string workflowId, CancellationToken cancellationToken = default)
    {
        await _indexInitializer.EnsureAsync(domainName, cancellationToken);
        return await Collection(domainName)
            .Find(x => x.WorkflowId == workflowId && x.Status == WorkflowVersionStatus.Published)
            .SortByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowVersionDocument>> ListByWorkflowIdAsync(string domainName, string workflowId, CancellationToken cancellationToken = default)
    {
        await _indexInitializer.EnsureAsync(domainName, cancellationToken);
        return await Collection(domainName)
            .Find(x => x.WorkflowId == workflowId)
            .SortByDescending(x => x.Version)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetMaxVersionNumberAsync(string domainName, string workflowId, CancellationToken cancellationToken = default)
    {
        await _indexInitializer.EnsureAsync(domainName, cancellationToken);
        var latest = await Collection(domainName)
            .Find(x => x.WorkflowId == workflowId)
            .SortByDescending(x => x.Version)
            .Limit(1)
            .Project(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);
        return latest;
    }

    public async Task<bool> ReplaceAsync(WorkflowVersionDocument version, CancellationToken cancellationToken = default)
    {
        var result = await Collection(version.DomainName).ReplaceOneAsync(
            x => x.Id == version.Id && x.Status == WorkflowVersionStatus.Draft,
            version,
            cancellationToken: cancellationToken);
        return result.IsAcknowledged && result.ModifiedCount == 1;
    }

    public async Task ArchivePublishedExceptAsync(string domainName, string workflowId, string exceptVersionId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<WorkflowVersionDocument>.Filter.Where(x =>
            x.WorkflowId == workflowId &&
            x.Status == WorkflowVersionStatus.Published &&
            x.Id != exceptVersionId);

        var update = Builders<WorkflowVersionDocument>.Update
            .Set(x => x.Status, WorkflowVersionStatus.Archived)
            .Set(x => x.UpdatedAt, DateTime.UtcNow);

        await Collection(domainName).UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
    }

    private IMongoCollection<WorkflowVersionDocument> Collection(string domainName) =>
        _context.GetDatabase(domainName).GetCollection<WorkflowVersionDocument>(WorkflowCollectionNames.Versions);
}

public sealed class WorkflowInstanceRepository : IWorkflowInstanceRepository
{
    private readonly IWorkflowMongoContext _context;
    private readonly WorkflowIndexInitializer _indexInitializer;

    public WorkflowInstanceRepository(IWorkflowMongoContext context, WorkflowIndexInitializer indexInitializer)
    {
        _context = context;
        _indexInitializer = indexInitializer;
    }

    public async Task InsertAsync(WorkflowInstanceDocument instance, CancellationToken cancellationToken = default)
    {
        await _indexInitializer.EnsureAsync(instance.DomainName, cancellationToken);
        await Collection(instance.DomainName).InsertOneAsync(instance, cancellationToken: cancellationToken);
    }

    public async Task<WorkflowInstanceDocument?> GetByIdAsync(string domainName, string instanceId, CancellationToken cancellationToken = default)
    {
        await _indexInitializer.EnsureAsync(domainName, cancellationToken);
        return await Collection(domainName).Find(x => x.Id == instanceId).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> TryUpdateAsync(WorkflowInstanceDocument instance, long expectedRevision, CancellationToken cancellationToken = default)
    {
        var result = await Collection(instance.DomainName).ReplaceOneAsync(
            x => x.Id == instance.Id && x.Revision == expectedRevision,
            instance,
            cancellationToken: cancellationToken);
        return result.IsAcknowledged && result.ModifiedCount == 1;
    }

    public async Task<IReadOnlyList<WorkflowInstanceDocument>> ListAsync(
        string domainName,
        string? workflowId,
        WorkflowInstanceStatus? status,
        int skip,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await _indexInitializer.EnsureAsync(domainName, cancellationToken);
        var filter = Builders<WorkflowInstanceDocument>.Filter.Empty;
        if (!string.IsNullOrWhiteSpace(workflowId))
            filter &= Builders<WorkflowInstanceDocument>.Filter.Eq(x => x.WorkflowId, workflowId);
        if (status.HasValue)
            filter &= Builders<WorkflowInstanceDocument>.Filter.Eq(x => x.Status, status.Value);

        var cappedLimit = Math.Clamp(limit, 1, 200);
        return await Collection(domainName)
            .Find(filter)
            .SortByDescending(x => x.StartedAt)
            .Skip(skip)
            .Limit(cappedLimit)
            .ToListAsync(cancellationToken);
    }

    private IMongoCollection<WorkflowInstanceDocument> Collection(string domainName) =>
        _context.GetDatabase(domainName).GetCollection<WorkflowInstanceDocument>(WorkflowCollectionNames.Instances);
}

public sealed class NodeExecutionRepository : INodeExecutionRepository
{
    private readonly IWorkflowMongoContext _context;
    private readonly WorkflowIndexInitializer _indexInitializer;

    public NodeExecutionRepository(IWorkflowMongoContext context, WorkflowIndexInitializer indexInitializer)
    {
        _context = context;
        _indexInitializer = indexInitializer;
    }

    public async Task<bool> IsSuccessfulAsync(string domainName, string instanceId, string nodeId, int attempt, CancellationToken cancellationToken = default)
    {
        await _indexInitializer.EnsureAsync(domainName, cancellationToken);
        var col = _context.GetDatabase(domainName).GetCollection<NodeExecutionDocument>(WorkflowCollectionNames.NodeExecutions);
        var filter = Builders<NodeExecutionDocument>.Filter.Where(x =>
            x.InstanceId == instanceId &&
            x.NodeId == nodeId &&
            x.Attempt == attempt &&
            x.Status == NodeExecutionStatus.Success);

        return await col.Find(filter).Limit(1).Project(x => x.Id).AnyAsync(cancellationToken);
    }

    public async Task InsertAsync(NodeExecutionDocument execution, CancellationToken cancellationToken = default)
    {
        await _indexInitializer.EnsureAsync(execution.DomainName, cancellationToken);
        var col = _context.GetDatabase(execution.DomainName).GetCollection<NodeExecutionDocument>(WorkflowCollectionNames.NodeExecutions);
        await col.InsertOneAsync(execution, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<NodeExecutionDocument>> ListByInstanceAsync(string domainName, string instanceId, CancellationToken cancellationToken = default)
    {
        await _indexInitializer.EnsureAsync(domainName, cancellationToken);
        var col = _context.GetDatabase(domainName).GetCollection<NodeExecutionDocument>(WorkflowCollectionNames.NodeExecutions);
        return await col.Find(x => x.InstanceId == instanceId)
            .SortBy(x => x.StartedAt)
            .ToListAsync(cancellationToken);
    }
}

public sealed class WorkflowTriggerRepository : IWorkflowTriggerRepository
{
    private readonly IWorkflowMongoContext _context;
    private readonly WorkflowIndexInitializer _indexInitializer;

    public WorkflowTriggerRepository(IWorkflowMongoContext context, WorkflowIndexInitializer indexInitializer)
    {
        _context = context;
        _indexInitializer = indexInitializer;
    }

    public async Task ReplaceForWorkflowAsync(
        string domainName,
        string workflowId,
        IReadOnlyList<WorkflowTriggerProjectionDocument> triggers,
        CancellationToken cancellationToken = default)
    {
        await _indexInitializer.EnsureAsync(domainName, cancellationToken);
        var col = Collection(domainName);

        await col.DeleteManyAsync(x => x.WorkflowId == workflowId, cancellationToken);
        if (triggers.Count > 0)
            await col.InsertManyAsync(triggers, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowTriggerProjectionDocument>> FindByEventTypeAsync(
        string domainName,
        string eventType,
        CancellationToken cancellationToken = default)
    {
        await _indexInitializer.EnsureAsync(domainName, cancellationToken);
        return await Collection(domainName)
            .Find(x => x.EventType == eventType && x.Enabled)
            .ToListAsync(cancellationToken);
    }

    private IMongoCollection<WorkflowTriggerProjectionDocument> Collection(string domainName) =>
        _context.GetDatabase(domainName).GetCollection<WorkflowTriggerProjectionDocument>(WorkflowCollectionNames.Triggers);
}

public sealed class WorkflowApprovalRepository : IWorkflowApprovalRepository
{
    private readonly IWorkflowMongoContext _context;
    private readonly WorkflowIndexInitializer _indexInitializer;

    public WorkflowApprovalRepository(IWorkflowMongoContext context, WorkflowIndexInitializer indexInitializer)
    {
        _context = context;
        _indexInitializer = indexInitializer;
    }

    public async Task InsertAsync(WorkflowApprovalDocument approval, CancellationToken cancellationToken = default)
    {
        await _indexInitializer.EnsureAsync(approval.DomainName, cancellationToken);
        await Collection(approval.DomainName).InsertOneAsync(approval, cancellationToken: cancellationToken);
    }

    public async Task<WorkflowApprovalDocument?> GetByIdAsync(string domainName, string approvalId, CancellationToken cancellationToken = default)
    {
        await _indexInitializer.EnsureAsync(domainName, cancellationToken);
        return await Collection(domainName)
            .Find(x => x.Id == approvalId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<WorkflowApprovalDocument?> GetPendingByInstanceNodeAsync(
        string domainName,
        string instanceId,
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        await _indexInitializer.EnsureAsync(domainName, cancellationToken);
        return await Collection(domainName)
            .Find(x => x.InstanceId == instanceId && x.NodeId == nodeId && x.Status == WorkflowApprovalStatus.Pending)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> UpdateAsync(WorkflowApprovalDocument approval, CancellationToken cancellationToken = default)
    {
        var result = await Collection(approval.DomainName)
            .ReplaceOneAsync(x => x.Id == approval.Id, approval, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0 || result.MatchedCount > 0;
    }

    public async Task<IReadOnlyList<WorkflowApprovalDocument>> ListAsync(
        string domainName,
        WorkflowApprovalStatus? status,
        int skip,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await _indexInitializer.EnsureAsync(domainName, cancellationToken);
        var filter = status.HasValue
            ? Builders<WorkflowApprovalDocument>.Filter.Eq(x => x.Status, status.Value)
            : FilterDefinition<WorkflowApprovalDocument>.Empty;

        return await Collection(domainName)
            .Find(filter)
            .SortByDescending(x => x.CreatedAt)
            .Skip(skip)
            .Limit(limit)
            .ToListAsync(cancellationToken);
    }

    private IMongoCollection<WorkflowApprovalDocument> Collection(string domainName) =>
        _context.GetDatabase(domainName).GetCollection<WorkflowApprovalDocument>(WorkflowCollectionNames.Approvals);
}

public sealed class WorkflowSecretRepository : IWorkflowSecretRepository
{
    private readonly IWorkflowMongoContext _context;
    private readonly WorkflowIndexInitializer _indexInitializer;

    public WorkflowSecretRepository(IWorkflowMongoContext context, WorkflowIndexInitializer indexInitializer)
    {
        _context = context;
        _indexInitializer = indexInitializer;
    }

    public async Task InsertAsync(WorkflowSecretDocument secret, CancellationToken cancellationToken = default)
    {
        await _indexInitializer.EnsureAsync(secret.DomainName, cancellationToken);
        await Collection(secret.DomainName).InsertOneAsync(secret, cancellationToken: cancellationToken);
    }

    public async Task<WorkflowSecretDocument?> GetByKeyAsync(string domainName, string key, CancellationToken cancellationToken = default)
    {
        await _indexInitializer.EnsureAsync(domainName, cancellationToken);
        return await Collection(domainName)
            .Find(x => x.Key == key)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkflowSecretDocument>> ListAsync(string domainName, CancellationToken cancellationToken = default)
    {
        await _indexInitializer.EnsureAsync(domainName, cancellationToken);
        return await Collection(domainName)
            .Find(FilterDefinition<WorkflowSecretDocument>.Empty)
            .SortBy(x => x.Key)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ReplaceAsync(WorkflowSecretDocument secret, CancellationToken cancellationToken = default)
    {
        var result = await Collection(secret.DomainName)
            .ReplaceOneAsync(x => x.Id == secret.Id, secret, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0 || result.MatchedCount > 0;
    }

    private IMongoCollection<WorkflowSecretDocument> Collection(string domainName) =>
        _context.GetDatabase(domainName).GetCollection<WorkflowSecretDocument>(WorkflowCollectionNames.Secrets);
}
