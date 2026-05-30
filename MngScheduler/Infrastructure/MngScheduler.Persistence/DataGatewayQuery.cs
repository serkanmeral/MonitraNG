namespace MngScheduler.Persistence;

/// <summary>
/// MngDataGateway list query helpers (filter: field:eq:value, sort: -field desc).
/// </summary>
internal static class DataGatewayQuery
{
    public static string Eq(string field, string value) => $"{field}:eq:{value}";

    public static string BuildListQuery(
        IReadOnlyList<(string Field, string Value)> eqFilters,
        string? sortFieldDesc = null,
        int? limit = null)
    {
        var filter = string.Join(",", eqFilters.Select(f => Eq(f.Field, f.Value)));
        var parts = new List<string> { $"filter={Uri.EscapeDataString(filter)}" };

        if (!string.IsNullOrWhiteSpace(sortFieldDesc))
        {
            parts.Add($"sort=-{sortFieldDesc}");
        }

        if (limit is > 0)
        {
            parts.Add($"limit={limit.Value}");
        }

        return string.Join("&", parts);
    }
}
