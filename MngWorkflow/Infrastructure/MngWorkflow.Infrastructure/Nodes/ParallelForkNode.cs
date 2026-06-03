using System.Text.Json;
using MngWorkflow.Application.Execution;
using MngWorkflow.Application.Nodes;
using MngWorkflow.Domain.Constants;
using MngWorkflow.Domain.Entities;

namespace MngWorkflow.Infrastructure.Nodes;

/// <summary>
/// Fans out to multiple branches via configured edge keys (parallel execution).
/// </summary>
public sealed class ParallelForkNode : IWorkflowNode
{
    public string NodeType => WorkflowNodeTypes.ParallelFork;

    public Task<NodeExecutionResult> ExecuteAsync(
        WorkflowExecutionContext context,
        WorkflowNodeDefinition node,
        CancellationToken cancellationToken)
    {
        var branches = ReadBranchKeys(node);
        if (branches.Count == 0)
            return Task.FromResult(NodeExecutionResult.Fail("parallel.fork: config.branches (string[]) is required", retryable: false));

        return Task.FromResult(NodeExecutionResult.Ok(nextEdges: branches));
    }

    private static List<string> ReadBranchKeys(WorkflowNodeDefinition node)
    {
        if (!node.Config.TryGetValue("branches", out var raw) || raw == null)
            return [];

        if (raw is JsonElement element && element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray()
                .Select(x => x.GetString()?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Cast<string>()
                .ToList();
        }

        if (raw is IEnumerable<object> list)
        {
            return list
                .Select(x => x?.ToString()?.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Cast<string>()
                .ToList();
        }

        return [];
    }
}
