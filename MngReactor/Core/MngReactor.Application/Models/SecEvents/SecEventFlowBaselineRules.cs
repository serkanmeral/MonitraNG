namespace MngReactor.Application.Models.SecEvents;

/// <summary>U7 baseline kuralları — src→dst akış eylemleri.</summary>
public static class SecEventFlowBaselineRules
{
    public const string NewFlowAction = "new_flow";

    private static readonly HashSet<string> FlowActions =
        new(StringComparer.OrdinalIgnoreCase) { "allowed_flow", "denied_flow" };

    public static bool IsFlowAction(string? eventAction) =>
        !string.IsNullOrWhiteSpace(eventAction) && FlowActions.Contains(eventAction);
}
