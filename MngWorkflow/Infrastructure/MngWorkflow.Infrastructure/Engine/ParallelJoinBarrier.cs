using MngWorkflow.Domain.Entities;

namespace MngWorkflow.Infrastructure.Engine;

/// <summary>
/// Tracks inbound branch arrivals per join node in instance execution context.
/// </summary>
public static class ParallelJoinBarrier
{
    private const string ContextKey = "parallelJoin";

    public static bool TryRegisterArrival(
        WorkflowInstanceDocument instance,
        WorkflowVersionDocument version,
        string joinNodeId,
        string fromNodeId,
        out int arrivedCount,
        out int expectedCount)
    {
        expectedCount = version.Edges.Count(e => string.Equals(e.ToNodeId, joinNodeId, StringComparison.Ordinal));
        if (expectedCount <= 0)
            expectedCount = 1;

        var joins = GetJoinMap(instance);
        if (!joins.TryGetValue(joinNodeId, out var stateObj) || stateObj is not Dictionary<string, object?> state)
        {
            state = new Dictionary<string, object?>(StringComparer.Ordinal);
            joins[joinNodeId] = state;
        }

        var arrived = ReadArrived(state);
        if (!arrived.Contains(fromNodeId, StringComparer.Ordinal))
            arrived.Add(fromNodeId);

        state["arrived"] = arrived;
        state["expected"] = expectedCount;
        arrivedCount = arrived.Count;
        return arrivedCount >= expectedCount;
    }

    public static void Clear(WorkflowInstanceDocument instance, string joinNodeId)
    {
        if (!instance.ExecutionContext.TryGetValue(ContextKey, out var root) ||
            root is not Dictionary<string, object?> joins)
            return;

        joins.Remove(joinNodeId);
    }

    private static Dictionary<string, object?> GetJoinMap(WorkflowInstanceDocument instance)
    {
        if (!instance.ExecutionContext.TryGetValue(ContextKey, out var root) ||
            root is not Dictionary<string, object?> joins)
        {
            joins = new Dictionary<string, object?>(StringComparer.Ordinal);
            instance.ExecutionContext[ContextKey] = joins;
        }

        return joins;
    }

    private static List<string> ReadArrived(Dictionary<string, object?> state)
    {
        if (!state.TryGetValue("arrived", out var raw) || raw == null)
            return [];

        if (raw is List<string> strings)
            return strings;

        if (raw is IEnumerable<object?> list)
            return list.Select(x => x?.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToList();

        return [];
    }
}
