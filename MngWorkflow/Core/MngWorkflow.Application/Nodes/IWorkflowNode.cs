using MngWorkflow.Application.Execution;
using MngWorkflow.Domain.Entities;

namespace MngWorkflow.Application.Nodes;

public interface IWorkflowNode
{
    string NodeType { get; }

    Task<NodeExecutionResult> ExecuteAsync(
        WorkflowExecutionContext context,
        WorkflowNodeDefinition node,
        CancellationToken cancellationToken);
}

public interface INodeRegistry
{
    IWorkflowNode Resolve(string nodeType);
}
