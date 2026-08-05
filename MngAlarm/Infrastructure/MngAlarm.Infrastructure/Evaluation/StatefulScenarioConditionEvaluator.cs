using MngAlarm.Application.Observations;
using MngAlarm.Domain.Entities;
using MngAlarm.Infrastructure.State;

namespace MngAlarm.Infrastructure.Evaluation;

public static class StatefulScenarioConditionEvaluator
{
    public static bool Matches(
        AlarmRuleDocument rule,
        ScenarioCondition? condition,
        ObservationEnvelope observation,
        ISequenceStateStore states,
        string path = "root")
    {
        if (condition == null)
            return true;

        bool matched;
        if (!string.IsNullOrWhiteSpace(condition.Logic))
        {
            var childResults = condition.Children
                .Select((child, index) => Matches(rule, child, observation, states, $"{path}.{index}"))
                .ToList();
            matched = condition.Logic.ToUpperInvariant() switch
            {
                "AND" => childResults.All(x => x),
                "OR" => childResults.Any(x => x),
                "NOT" => childResults.Count == 1 && !childResults[0],
                _ => false
            };
        }
        else
        {
            matched = ScenarioCompiler.Matches(condition, observation, out _);
        }

        if (condition.SustainedForSeconds <= 0)
            return matched;

        var groupKey = CorrelationEvaluator.BuildGroupKey(rule, observation.Dimensions);
        var stateKey = $"{SequenceEvaluator.BuildStoreKey(observation.DomainName, rule.Id, groupKey)}:sustained:{path}";
        if (!matched)
        {
            states.Reset(stateKey);
            return false;
        }

        var state = states.GetOrCreate(stateKey);
        state.ConditionSince ??= observation.Timestamp;
        states.Save(stateKey, state);
        return observation.Timestamp - state.ConditionSince.Value >= TimeSpan.FromSeconds(condition.SustainedForSeconds);
    }

    public static bool ApplyHysteresis(
        ScenarioHysteresis hysteresis,
        double value,
        bool baseConditionMatches,
        DateTime? openSince,
        DateTime now)
    {
        if (!openSince.HasValue)
            return baseConditionMatches && value >= hysteresis.RaiseThreshold;

        var minimumElapsed = now - openSince.Value >= TimeSpan.FromSeconds(hysteresis.MinimumStateSeconds);
        var shouldClear = !baseConditionMatches || value <= hysteresis.ClearThreshold;
        return !(shouldClear && minimumElapsed);
    }
}
