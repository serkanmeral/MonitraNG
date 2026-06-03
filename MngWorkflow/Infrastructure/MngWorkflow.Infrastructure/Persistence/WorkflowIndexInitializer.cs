using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MngWorkflow.Domain.Constants;
using MngWorkflow.Domain.Entities;
using MngWorkflow.Domain.Enums;

namespace MngWorkflow.Infrastructure.Persistence;

/// <summary>Domain DB başına indeksler — idempotent, worker start'ta bir kez.</summary>
public sealed class WorkflowIndexInitializer
{
    private readonly IWorkflowMongoContext _context;
    private readonly ILogger<WorkflowIndexInitializer> _logger;
    private readonly HashSet<string> _initialized = new(StringComparer.OrdinalIgnoreCase);
    private readonly Lock _lock = new();

    public WorkflowIndexInitializer(IWorkflowMongoContext context, ILogger<WorkflowIndexInitializer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task EnsureAsync(string domainName, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!_initialized.Add(domainName))
                return;
        }

        var db = _context.GetDatabase(domainName);

        await EnsureInstanceIndexesAsync(db, cancellationToken);
        await EnsureExecutionIndexesAsync(db, cancellationToken);
        await EnsureVersionIndexesAsync(db, cancellationToken);
        await EnsureDefinitionIndexesAsync(db, cancellationToken);
        await EnsureTriggerIndexesAsync(db, cancellationToken);
        await EnsureApprovalIndexesAsync(db, cancellationToken);
        await EnsureSecretIndexesAsync(db, cancellationToken);
    }

    private async Task EnsureInstanceIndexesAsync(IMongoDatabase db, CancellationToken cancellationToken)
    {
        var col = db.GetCollection<WorkflowInstanceDocument>(WorkflowCollectionNames.Instances);
        var models = new[]
        {
            new CreateIndexModel<WorkflowInstanceDocument>(
                Builders<WorkflowInstanceDocument>.IndexKeys
                    .Ascending(x => x.WorkflowId)
                    .Descending(x => x.StartedAt),
                new CreateIndexOptions { Name = "idx_workflow_started" })
        };
        await col.Indexes.CreateManyAsync(models, cancellationToken);
    }

    private async Task EnsureExecutionIndexesAsync(IMongoDatabase db, CancellationToken cancellationToken)
    {
        var col = db.GetCollection<NodeExecutionDocument>(WorkflowCollectionNames.NodeExecutions);
        var models = new[]
        {
            new CreateIndexModel<NodeExecutionDocument>(
                Builders<NodeExecutionDocument>.IndexKeys
                    .Ascending(x => x.InstanceId)
                    .Ascending(x => x.NodeId)
                    .Ascending(x => x.Attempt),
                new CreateIndexOptions { Name = "idx_instance_node_attempt", Unique = true }),
            new CreateIndexModel<NodeExecutionDocument>(
                Builders<NodeExecutionDocument>.IndexKeys.Ascending(x => x.InstanceId),
                new CreateIndexOptions { Name = "idx_instance" })
        };
        await col.Indexes.CreateManyAsync(models, cancellationToken);
    }

    private async Task EnsureVersionIndexesAsync(IMongoDatabase db, CancellationToken cancellationToken)
    {
        var col = db.GetCollection<WorkflowVersionDocument>(WorkflowCollectionNames.Versions);
        var models = new[]
        {
            new CreateIndexModel<WorkflowVersionDocument>(
                Builders<WorkflowVersionDocument>.IndexKeys
                    .Ascending(x => x.WorkflowId)
                    .Ascending(x => x.Version),
                new CreateIndexOptions { Name = "idx_workflow_version" })
        };
        await col.Indexes.CreateManyAsync(models, cancellationToken);
    }

    private async Task EnsureDefinitionIndexesAsync(IMongoDatabase db, CancellationToken cancellationToken)
    {
        var col = db.GetCollection<WorkflowDefinitionDocument>(WorkflowCollectionNames.Definitions);
        var models = new[]
        {
            new CreateIndexModel<WorkflowDefinitionDocument>(
                Builders<WorkflowDefinitionDocument>.IndexKeys
                    .Ascending(x => x.DomainId)
                    .Ascending(x => x.Key),
                new CreateIndexOptions { Name = "idx_domain_key", Unique = true })
        };
        await col.Indexes.CreateManyAsync(models, cancellationToken);
    }

    private async Task EnsureTriggerIndexesAsync(IMongoDatabase db, CancellationToken cancellationToken)
    {
        var col = db.GetCollection<WorkflowTriggerProjectionDocument>(WorkflowCollectionNames.Triggers);
        var models = new[]
        {
            new CreateIndexModel<WorkflowTriggerProjectionDocument>(
                Builders<WorkflowTriggerProjectionDocument>.IndexKeys
                    .Ascending(x => x.EventType)
                    .Ascending(x => x.WorkflowId),
                new CreateIndexOptions { Name = "idx_event_workflow" })
        };
        await col.Indexes.CreateManyAsync(models, cancellationToken);
    }

    private async Task EnsureApprovalIndexesAsync(IMongoDatabase db, CancellationToken cancellationToken)
    {
        var col = db.GetCollection<WorkflowApprovalDocument>(WorkflowCollectionNames.Approvals);
        var models = new[]
        {
            new CreateIndexModel<WorkflowApprovalDocument>(
                Builders<WorkflowApprovalDocument>.IndexKeys
                    .Ascending(x => x.InstanceId)
                    .Ascending(x => x.NodeId)
                    .Ascending(x => x.Status),
                new CreateIndexOptions { Name = "idx_instance_node_status" }),
            new CreateIndexModel<WorkflowApprovalDocument>(
                Builders<WorkflowApprovalDocument>.IndexKeys
                    .Ascending(x => x.Status)
                    .Descending(x => x.CreatedAt),
                new CreateIndexOptions { Name = "idx_status_created" })
        };
        await col.Indexes.CreateManyAsync(models, cancellationToken);
    }

    private async Task EnsureSecretIndexesAsync(IMongoDatabase db, CancellationToken cancellationToken)
    {
        var col = db.GetCollection<WorkflowSecretDocument>(WorkflowCollectionNames.Secrets);
        var models = new[]
        {
            new CreateIndexModel<WorkflowSecretDocument>(
                Builders<WorkflowSecretDocument>.IndexKeys.Ascending(x => x.Key),
                new CreateIndexOptions { Name = "idx_secret_key", Unique = true })
        };
        await col.Indexes.CreateManyAsync(models, cancellationToken);
    }
}
