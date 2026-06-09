namespace MngOperations.Domain.Constants;

public static class OcQueries
{
    public static readonly HashSet<string> WorkItems = new(StringComparer.OrdinalIgnoreCase)
    {
        "wi_by_workspace_and_state",
        "wi_board_column",
        "wi_assigned_to_user",
        "wi_assigned_open",
        "wi_sla_response_breach",
        "wi_sla_resolve_breach",
        "wi_count_by_state"
    };

    public static bool IsAllowed(string dataset, string queryKey) =>
        string.Equals(dataset, OcDatasets.WorkItems, StringComparison.OrdinalIgnoreCase)
        && WorkItems.Contains(queryKey);
}
