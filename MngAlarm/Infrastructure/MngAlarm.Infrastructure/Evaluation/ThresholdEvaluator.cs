using MngAlarm.Domain.Entities;

namespace MngAlarm.Infrastructure.Evaluation;

public static class ThresholdEvaluator
{
    public static bool Matches(AlarmRuleDocument rule, double? value)
    {
        if (value == null || rule.Type != Domain.Constants.AlarmRuleTypes.Threshold)
            return false;

        return rule.Operator switch
        {
            "gt" => value > rule.Threshold,
            "gte" => value >= rule.Threshold,
            "lt" => value < rule.Threshold,
            "lte" => value <= rule.Threshold,
            "eq" => Math.Abs(value.Value - rule.Threshold) < 0.000_001,
            _ => value > rule.Threshold
        };
    }

    public static string BuildDedupKey(AlarmRuleDocument rule, string observationKey) =>
        rule.DedupKeyTemplate
            .Replace("{ruleId}", rule.Id, StringComparison.Ordinal)
            .Replace("{key}", observationKey, StringComparison.Ordinal);
}
