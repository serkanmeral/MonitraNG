namespace MngReactor.Application.Models.SecEvents;

/// <summary>Distinct scope values for SIEM Events filter comboboxes.</summary>
public sealed class SecEventScopeOptions
{
    public IReadOnlyList<string> Types { get; init; } = [];
    public IReadOnlyList<string> Products { get; init; } = [];
    public IReadOnlyList<string> Hosts { get; init; } = [];
    /// <summary>Lookback window used for aggregation.</summary>
    public int RangeHours { get; init; }
    public string Source { get; init; } = "unknown";
}
