using MngWorkflow.Domain.Entities;

namespace MngWorkflow.Infrastructure.Validation;

public static class WorkflowGraphValidator
{
    public static void Validate(string entryNodeId, IReadOnlyList<WorkflowNodeDefinition> nodes, IReadOnlyList<WorkflowEdgeDefinition> edges)
    {
        if (string.IsNullOrWhiteSpace(entryNodeId))
            throw new ArgumentException("entryNodeId is required.");

        if (nodes.Count == 0)
            throw new ArgumentException("At least one node is required.");

        var nodeIds = new HashSet<string>(nodes.Select(n => n.Id), StringComparer.Ordinal);
        if (!nodeIds.Contains(entryNodeId))
            throw new ArgumentException($"entryNodeId '{entryNodeId}' not found in nodes.");

        foreach (var node in nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Id))
                throw new ArgumentException("Node id is required.");
            if (string.IsNullOrWhiteSpace(node.Type))
                throw new ArgumentException($"Node '{node.Id}' type is required.");
        }

        foreach (var edge in edges)
        {
            if (!nodeIds.Contains(edge.FromNodeId))
                throw new ArgumentException($"Edge from '{edge.FromNodeId}' references unknown node.");
            if (!nodeIds.Contains(edge.ToNodeId))
                throw new ArgumentException($"Edge to '{edge.ToNodeId}' references unknown node.");
        }
    }
}
