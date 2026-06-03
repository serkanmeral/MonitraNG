using MngWorkflow.Application.Execution;
using MngWorkflow.Application.Nodes;
using MngWorkflow.Domain.Constants;
using MngWorkflow.Domain.Entities;

namespace MngWorkflow.Infrastructure.Nodes;

/// <summary>
/// Synchronization point — engine barrier ensures all inbound branches arrive before this node runs.
/// </summary>
public sealed class ParallelJoinNode : IWorkflowNode
{
    public string NodeType => WorkflowNodeTypes.ParallelJoin;

    public Task<NodeExecutionResult> ExecuteAsync(
        WorkflowExecutionContext context,
        WorkflowNodeDefinition node,
        CancellationToken cancellationToken) =>
        Task.FromResult(NodeExecutionResult.Ok(output: new Dictionary<string, object?>
        {
            ["joined"] = true,
            ["nodeId"] = node.Id
        }));
}
