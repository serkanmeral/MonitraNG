namespace MngWorkflow.Infrastructure.Engine;

public static class WorkflowInstanceNavigator
{
    /// <summary>
    /// Removes completed node from active set, adds next nodes. Returns true when no active nodes remain.
    /// </summary>
    public static bool TryAdvanceActiveNodes(
        List<string> currentNodes,
        string completedNodeId,
        IReadOnlyList<string> nextNodeIds,
        out List<string> updatedActiveNodes)
    {
        var active = currentNodes
            .Where(n => !string.Equals(n, completedNodeId, StringComparison.Ordinal))
            .ToList();

        if (nextNodeIds.Count > 0)
            active.AddRange(nextNodeIds);

        updatedActiveNodes = active.Distinct(StringComparer.Ordinal).ToList();
        return updatedActiveNodes.Count == 0;
    }
}
