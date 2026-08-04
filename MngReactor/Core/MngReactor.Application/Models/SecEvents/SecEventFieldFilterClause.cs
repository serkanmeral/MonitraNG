namespace MngReactor.Application.Models.SecEvents;

/// <summary>
/// Generic field filter clause from SIEM Events UI (maps to target-field catalog names).
/// </summary>
public sealed class SecEventFieldFilterClause
{
    /// <summary>Catalog field name (e.g. actor.user, custom.session_id, message).</summary>
    public required string Field { get; init; }

    /// <summary>eq | neq | in | contains | prefix</summary>
    public required string Op { get; init; }

    /// <summary>Single value, or CSV for <c>in</c>.</summary>
    public required string Value { get; init; }
}
