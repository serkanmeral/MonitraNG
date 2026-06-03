using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngWorkflow.Application.Configuration;
using MngWorkflow.Application.Execution;
using MngWorkflow.Application.Nodes;
using MngWorkflow.Application.Repositories;
using MngWorkflow.Application.Services;
using MngWorkflow.Domain.Constants;
using MngWorkflow.Domain.Entities;
using MngWorkflow.Domain.Enums;

namespace MngWorkflow.Infrastructure.Engine;

public sealed class WorkflowExecutionEngine : IWorkflowExecutionEngine
{
    private readonly IWorkflowInstanceRepository _instances;
    private readonly IWorkflowVersionRepository _versions;
    private readonly INodeExecutionRepository _executions;
    private readonly INodeRegistry _registry;
    private readonly IWorkflowQueuePublisher _publisher;
    private readonly EngineSettings _engine;
    private readonly ILogger<WorkflowExecutionEngine> _logger;

    public WorkflowExecutionEngine(
        IWorkflowInstanceRepository instances,
        IWorkflowVersionRepository versions,
        INodeExecutionRepository executions,
        INodeRegistry registry,
        IWorkflowQueuePublisher publisher,
        IOptions<MngWorkflowSettings> settings,
        ILogger<WorkflowExecutionEngine> logger)
    {
        _instances = instances;
        _versions = versions;
        _executions = executions;
        _registry = registry;
        _publisher = publisher;
        _engine = settings.Value.Engine;
        _logger = logger;
    }

    public async Task ProcessMessageAsync(WorkflowExecutionMessage message, CancellationToken cancellationToken = default)
    {
        if (await _executions.IsSuccessfulAsync(message.DomainName, message.InstanceId, message.NodeId, message.Attempt, cancellationToken))
        {
            _logger.LogDebug("Skip duplicate success instance={InstanceId} node={NodeId} attempt={Attempt}",
                message.InstanceId, message.NodeId, message.Attempt);
            return;
        }

        var instance = await _instances.GetByIdAsync(message.DomainName, message.InstanceId, cancellationToken);
        if (instance == null)
        {
            _logger.LogWarning("Instance not found {InstanceId}", message.InstanceId);
            return;
        }

        if (instance.Status is WorkflowInstanceStatus.Completed or WorkflowInstanceStatus.Failed or WorkflowInstanceStatus.Cancelled)
            return;

        var version = await _versions.GetByIdAsync(message.DomainName, message.WorkflowVersionId, cancellationToken);
        if (version == null)
        {
            await FailInstanceAsync(instance, $"Version {message.WorkflowVersionId} not found", cancellationToken);
            return;
        }

        var nodeDef = version.Nodes.FirstOrDefault(n => n.Id == message.NodeId);
        if (nodeDef == null)
        {
            await FailInstanceAsync(instance, $"Node {message.NodeId} not found", cancellationToken);
            return;
        }

        var execution = new NodeExecutionDocument
        {
            InstanceId = instance.Id,
            DomainId = instance.DomainId,
            DomainName = instance.DomainName,
            NodeId = message.NodeId,
            Attempt = message.Attempt,
            Status = NodeExecutionStatus.Running,
            StartedAt = DateTime.UtcNow
        };

        NodeExecutionResult result;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_engine.NodeTimeoutSeconds));

            var context = BuildContext(instance);
            var executor = _registry.Resolve(nodeDef.Type);
            result = await executor.ExecuteAsync(context, nodeDef, cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Node execution error instance={InstanceId} node={NodeId}", instance.Id, message.NodeId);
            result = NodeExecutionResult.Fail(ex.Message);
        }

        execution.FinishedAt = DateTime.UtcNow;
        execution.Output = result.Output;
        execution.ErrorMessage = result.ErrorMessage;

        if (result.ShouldWait)
        {
            execution.Status = NodeExecutionStatus.Success;
            execution.Output = result.Output;
            await _executions.InsertAsync(execution, cancellationToken);
            MergeOutputs(instance, message.NodeId, result.Output);
            if (!string.IsNullOrWhiteSpace(result.WaitingType))
                instance.ExecutionContext["waitingType"] = result.WaitingType;
            instance.Status = WorkflowInstanceStatus.Waiting;
            instance.CurrentNodes = [message.NodeId];
            await TryPersistInstanceAsync(instance, cancellationToken);
            return;
        }

        if (!result.Success)
        {
            execution.Status = NodeExecutionStatus.Failed;
            await _executions.InsertAsync(execution, cancellationToken);

            if (!result.Retryable)
            {
                await PublishDeadLetterAndFailAsync(instance, message, result.ErrorMessage ?? "Node failed (non-retryable)", cancellationToken);
                return;
            }

            if (message.Attempt < _engine.MaxAttempts)
            {
                await _publisher.PublishRetryAsync(new WorkflowExecutionMessage
                {
                    InstanceId = message.InstanceId,
                    WorkflowVersionId = message.WorkflowVersionId,
                    NodeId = message.NodeId,
                    Attempt = message.Attempt + 1,
                    CorrelationId = message.CorrelationId,
                    DomainId = message.DomainId,
                    DomainName = message.DomainName
                }, message.Attempt, cancellationToken);
                return;
            }

            await PublishDeadLetterAndFailAsync(instance, message, result.ErrorMessage ?? "Node failed", cancellationToken);
            return;
        }

        execution.Status = NodeExecutionStatus.Success;
        await _executions.InsertAsync(execution, cancellationToken);

        MergeOutputs(instance, message.NodeId, result.Output);

        if (string.Equals(nodeDef.Type, WorkflowNodeTypes.ParallelJoin, StringComparison.Ordinal))
            ParallelJoinBarrier.Clear(instance, message.NodeId);

        var rawNextNodeIds = ResolveNextNodes(version, message.NodeId, result.NextEdges);
        var nextNodeIds = FilterNextNodesThroughJoinBarrier(instance, version, message.NodeId, rawNextNodeIds);

        var allBranchesDone = WorkflowInstanceNavigator.TryAdvanceActiveNodes(
            instance.CurrentNodes,
            message.NodeId,
            nextNodeIds,
            out var activeNodes);

        if (allBranchesDone)
        {
            instance.Status = WorkflowInstanceStatus.Completed;
            instance.CurrentNodes = [];
            instance.FinishedAt = DateTime.UtcNow;
            await TryPersistInstanceAsync(instance, cancellationToken);
            return;
        }

        instance.Status = WorkflowInstanceStatus.Running;
        instance.CurrentNodes = activeNodes;
        if (!await TryPersistInstanceAsync(instance, cancellationToken))
        {
            throw new InvalidOperationException("Optimistic concurrency conflict while updating instance.");
        }

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

    private static WorkflowExecutionContext BuildContext(WorkflowInstanceDocument instance)
    {
        var eventData = instance.ExecutionContext.TryGetValue("event", out var ev) && ev is Dictionary<string, object?> eventDict
            ? eventDict
            : instance.TriggerData;

        var variables = instance.ExecutionContext.TryGetValue("variables", out var vars) && vars is Dictionary<string, object?> varDict
            ? varDict
            : new Dictionary<string, object?>();

        var outputs = instance.ExecutionContext.TryGetValue("outputs", out var outs) && outs is Dictionary<string, object?> outDict
            ? outDict
            : new Dictionary<string, object?>();

        return new WorkflowExecutionContext
        {
            InstanceId = instance.Id,
            WorkflowVersionId = instance.WorkflowVersionId,
            DomainId = instance.DomainId,
            DomainName = instance.DomainName,
            CorrelationId = instance.CorrelationId,
            Event = eventData,
            Variables = variables,
            Outputs = outputs
        };
    }

    private static void MergeOutputs(WorkflowInstanceDocument instance, string nodeId, Dictionary<string, object?> output)
    {
        if (!instance.ExecutionContext.TryGetValue("outputs", out var existing) || existing is not Dictionary<string, object?> outputs)
        {
            outputs = new Dictionary<string, object?>(StringComparer.Ordinal);
            instance.ExecutionContext["outputs"] = outputs;
        }

        outputs[nodeId] = output;
    }

    private static List<string> ResolveNextNodes(WorkflowVersionDocument version, string nodeId, IReadOnlyList<string> edgeKeys)
    {
        var keys = edgeKeys.Count == 0 ? new[] { "default" } : edgeKeys;
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var edge in version.Edges.Where(e => e.FromNodeId == nodeId))
        {
            if (keys.Contains(edge.EdgeKey, StringComparer.Ordinal))
                set.Add(edge.ToNodeId);
        }

        return set.ToList();
    }

    private static List<string> FilterNextNodesThroughJoinBarrier(
        WorkflowInstanceDocument instance,
        WorkflowVersionDocument version,
        string completedNodeId,
        IReadOnlyList<string> rawNextNodeIds)
    {
        if (rawNextNodeIds.Count == 0)
            return [];

        var activated = new List<string>();
        foreach (var nextId in rawNextNodeIds)
        {
            var nextDef = version.Nodes.FirstOrDefault(n => n.Id == nextId);
            if (nextDef != null &&
                string.Equals(nextDef.Type, WorkflowNodeTypes.ParallelJoin, StringComparison.Ordinal))
            {
                if (ParallelJoinBarrier.TryRegisterArrival(
                        instance,
                        version,
                        nextId,
                        completedNodeId,
                        out _,
                        out _))
                    activated.Add(nextId);
            }
            else
            {
                activated.Add(nextId);
            }
        }

        return activated;
    }

    private async Task PublishDeadLetterAndFailAsync(
        WorkflowInstanceDocument instance,
        WorkflowExecutionMessage message,
        string reason,
        CancellationToken cancellationToken)
    {
        await _publisher.PublishDeadLetterAsync(new WorkflowDeadLetterMessage
        {
            Execution = message,
            Reason = reason
        }, cancellationToken);

        await FailInstanceAsync(instance, reason, cancellationToken);
    }

    private async Task FailInstanceAsync(WorkflowInstanceDocument instance, string reason, CancellationToken cancellationToken)
    {
        instance.Status = WorkflowInstanceStatus.Failed;
        instance.FinishedAt = DateTime.UtcNow;
        instance.CurrentNodes = [];
        await TryPersistInstanceAsync(instance, cancellationToken);
        _logger.LogWarning("Instance {InstanceId} failed: {Reason}", instance.Id, reason);
    }

    private async Task<bool> TryPersistInstanceAsync(WorkflowInstanceDocument instance, CancellationToken cancellationToken)
    {
        var expected = instance.Revision;
        instance.Revision = expected + 1;
        return await _instances.TryUpdateAsync(instance, expected, cancellationToken);
    }
}
