using System.Globalization;
using MngAlarm.Application.Observations;
using MngAlarm.Domain.Entities;
using MngAlarm.Infrastructure.State;

namespace MngAlarm.Infrastructure.Evaluation;

public static class SequenceEvaluator
{
    public static bool IsValidRule(AlarmRuleDocument rule) =>
        string.Equals(rule.Type, Domain.Constants.AlarmRuleTypes.Sequence, StringComparison.Ordinal)
        && rule.SequenceSteps.Count >= 2;

    public static string BuildStoreKey(string domainName, string ruleId, string groupKey) =>
        $"{domainName}:{ruleId}:{groupKey}";

    public static string BuildStepWindowKey(string storeKey, int stepIndex) =>
        $"{storeKey}:s{stepIndex}";

    public static Dictionary<string, object?> BuildContext(
        AlarmRuleDocument rule,
        ObservationEnvelope observation,
        string groupKey,
        SequenceRuntimeState state,
        int priorStepCount)
    {
        var ctx = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["kind"] = observation.Kind,
            ["key"] = rule.MatchKey,
            ["triggerKey"] = observation.Key,
            ["groupKey"] = groupKey,
            ["priorStepCount"] = priorStepCount,
            ["sequenceAnchor"] = state.AnchorTime?.ToString("O", CultureInfo.InvariantCulture)
        };

        if (observation.Value.HasValue)
            ctx["value"] = observation.Value.Value;

        foreach (var (k, v) in observation.Dimensions)
            ctx[k] = v;

        return ctx;
    }
}
