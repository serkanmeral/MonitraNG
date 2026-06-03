using System.Globalization;
using MngAlarm.Domain.Constants;
using MngAlarm.Domain.Entities;
using MngAlarm.Application.Observations;

namespace MngAlarm.Infrastructure.Evaluation;

public static class CorrelationEvaluator
{
    public static bool MatchesEvent(AlarmRuleDocument rule, ObservationEnvelope observation)
    {
        if (!string.Equals(rule.Type, AlarmRuleTypes.Correlation, StringComparison.Ordinal))
            return false;

        return string.Equals(observation.Key, rule.MatchKey, StringComparison.Ordinal);
    }

    public static string BuildGroupKey(AlarmRuleDocument rule, IReadOnlyDictionary<string, object?> dimensions)
    {
        if (rule.GroupByFields.Count == 0)
            return "_all";

        var parts = new List<string>(rule.GroupByFields.Count);
        foreach (var field in rule.GroupByFields)
        {
            dimensions.TryGetValue(field, out var value);
            parts.Add(value?.ToString() ?? string.Empty);
        }

        return string.Join("|", parts);
    }

    public static string BuildStoreKey(string domainName, string ruleId, string groupKey) =>
        $"{domainName}:{ruleId}:{groupKey}";

    public static string BuildDedupKey(AlarmRuleDocument rule, string groupKey) =>
        rule.DedupKeyTemplate
            .Replace("{ruleId}", rule.Id, StringComparison.Ordinal)
            .Replace("{groupKey}", groupKey, StringComparison.Ordinal)
            .Replace("{key}", rule.MatchKey, StringComparison.Ordinal);

    public static bool IsBreaching(int count, double threshold) =>
        count >= threshold;

    public static string BuildActivityKey(string domainName, string ruleId, string groupKey) =>
        $"{domainName}:{ruleId}:{groupKey}";

    public static TimeSpan GetWindow(AlarmRuleDocument rule) =>
        TimeSpan.FromMinutes(Math.Max(1, rule.WindowMinutes));

    public static Dictionary<string, object?> BuildContext(
        ObservationEnvelope observation,
        string groupKey,
        int windowCount)
    {
        var ctx = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["kind"] = observation.Kind,
            ["key"] = observation.Key,
            ["groupKey"] = groupKey,
            ["windowCount"] = windowCount,
            ["timestamp"] = observation.Timestamp.ToString("O", CultureInfo.InvariantCulture)
        };

        if (observation.Value.HasValue)
            ctx["value"] = observation.Value.Value;

        foreach (var (k, v) in observation.Dimensions)
            ctx[k] = v;

        return ctx;
    }
}
