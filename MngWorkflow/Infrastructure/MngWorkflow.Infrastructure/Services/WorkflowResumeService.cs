using Microsoft.Extensions.Logging;
using MngWorkflow.Application.Execution;
using MngWorkflow.Application.Repositories;
using MngWorkflow.Application.Services;
using MngWorkflow.Domain.Entities;
using MngWorkflow.Domain.Enums;

namespace MngWorkflow.Infrastructure.Services;

public sealed class WorkflowResumeService : IWorkflowResumeService
{
    private readonly IWorkflowInstanceRepository _instances;
    private readonly IWorkflowVersionRepository _versions;
    private readonly IWorkflowQueuePublisher _publisher;
    private readonly ILogger<WorkflowResumeService> _logger;

    public WorkflowResumeService(
        IWorkflowInstanceRepository instances,
        IWorkflowVersionRepository versions,
        IWorkflowQueuePublisher publisher,
        ILogger<WorkflowResumeService> logger)
    {
        _instances = instances;
        _versions = versions;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<WorkflowResumeResult> ResumeFromWaitingNodeAsync(
        string domainName,
        string instanceId,
        string nodeId,
        string edgeKey,
        Dictionary<string, object?>? additionalOutput = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(domainName))
            throw new ArgumentException("domainName is required.");
        if (string.IsNullOrWhiteSpace(instanceId))
            throw new ArgumentException("instanceId is required.");
        if (string.IsNullOrWhiteSpace(nodeId))
            throw new ArgumentException("nodeId is required.");

        var instance = await _instances.GetByIdAsync(domainName, instanceId, cancellationToken)
            ?? throw new KeyNotFoundException($"Instance '{instanceId}' not found.");

        if (instance.Status != WorkflowInstanceStatus.Waiting)
            throw new InvalidOperationException("Workflow instance is not waiting.");

        if (!instance.CurrentNodes.Contains(nodeId))
            throw new InvalidOperationException("Node is not the current waiting node.");

        var version = await _versions.GetByIdAsync(domainName, instance.WorkflowVersionId, cancellationToken)
            ?? throw new InvalidOperationException("Workflow version not found.");

        if (additionalOutput != null && additionalOutput.Count > 0)
            MergeOutputs(instance, nodeId, additionalOutput);

        var nextNodeIds = ResolveNextNodes(version, nodeId, [edgeKey]);
        var now = DateTime.UtcNow;
        WorkflowInstanceStatus finalStatus;

        if (nextNodeIds.Count == 0)
        {
            instance.Status = WorkflowInstanceStatus.Completed;
            instance.CurrentNodes = [];
            instance.FinishedAt = now;
            instance.ExecutionContext.Remove("waitingType");
            finalStatus = WorkflowInstanceStatus.Completed;
            await TryPersistInstanceAsync(instance, cancellationToken);
        }
        else
        {
            instance.Status = WorkflowInstanceStatus.Running;
            instance.CurrentNodes = nextNodeIds;
            instance.ExecutionContext.Remove("waitingType");
            finalStatus = WorkflowInstanceStatus.Running;

            if (!await TryPersistInstanceAsync(instance, cancellationToken))
                throw new InvalidOperationException("Optimistic concurrency conflict while resuming workflow.");

            foreach (var nextNodeId in nextNodeIds)
            {
                await _publisher.PublishExecutionAsync(new WorkflowExecutionMessage
                {
                    InstanceId = instance.Id,
                    WorkflowVersionId = version.Id,
                    NodeId = nextNodeId,
                    Attempt = 1,
                    CorrelationId = instance.CorrelationId,
                    DomainId = instance.DomainId,
                    DomainName = instance.DomainName
                }, cancellationToken);
            }
        }

        _logger.LogInformation(
            "Workflow resume instance={InstanceId} node={NodeId} edge={EdgeKey} status={Status} next={NextNodes}",
            instance.Id, nodeId, edgeKey, finalStatus, string.Join(",", nextNodeIds));

        return new WorkflowResumeResult(instance.Id, nodeId, edgeKey, finalStatus);
    }

    private static void MergeOutputs(WorkflowInstanceDocument instance, string nodeId, Dictionary<string, object?> output)
    {
        if (!instance.ExecutionContext.TryGetValue("outputs", out var existing) || existing is not Dictionary<string, object?> outputs)
        {
            outputs = new Dictionary<string, object?>(StringComparer.Ordinal);
            instance.ExecutionContext["outputs"] = outputs;
        }

        if (outputs.TryGetValue(nodeId, out var nodeOutput) && nodeOutput is Dictionary<string, object?> existingNodeOutput)
        {
            foreach (var pair in output)
                existingNodeOutput[pair.Key] = pair.Value;
        }
        else
        {
            outputs[nodeId] = new Dictionary<string, object?>(output);
        }
    }

    private static List<string> ResolveNextNodes(WorkflowVersionDocument version, string nodeId, IReadOnlyList<string> edgeKeys)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var edge in version.Edges.Where(e => e.FromNodeId == nodeId))
        {
            if (edgeKeys.Contains(edge.EdgeKey, StringComparer.Ordinal))
                set.Add(edge.ToNodeId);
        }

        return set.ToList();
    }

    private async Task<bool> TryPersistInstanceAsync(WorkflowInstanceDocument instance, CancellationToken cancellationToken)
    {
        var expected = instance.Revision;
        instance.Revision = expected + 1;
        return await _instances.TryUpdateAsync(instance, expected, cancellationToken);
    }
}
