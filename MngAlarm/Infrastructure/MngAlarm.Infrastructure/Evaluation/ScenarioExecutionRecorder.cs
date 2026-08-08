using MngAlarm.Application.Observations;
using MngAlarm.Domain.Entities;

namespace MngAlarm.Infrastructure.Evaluation;

public static class ScenarioExecutionRecorder
{
    public static bool ShouldPersist(
        string trigger,
        ScenarioGraphExecutionResult execution) =>
        string.Equals(trigger, ScenarioExecutionTriggers.Due, StringComparison.Ordinal)
        || execution.Traces.Any(x =>
            x.NodeType == ScenarioNodeTypes.Source
            && string.Equals(x.Status, "true", StringComparison.Ordinal));

    public static string ResolveOutcome(ScenarioGraphExecutionResult execution)
    {
        if (execution.Outputs.Count > 0)
            return ScenarioExecutionOutcomes.Matched;
        if (execution.Traces.Any(x => string.Equals(x.Status, "stopped", StringComparison.Ordinal)))
            return ScenarioExecutionOutcomes.Stopped;
        if (execution.PendingEvaluations.Count > 0
            || execution.Traces.Any(x => string.Equals(x.Status, "pending", StringComparison.Ordinal)))
            return ScenarioExecutionOutcomes.Pending;
        return ScenarioExecutionOutcomes.NoMatch;
    }

    public static ScenarioExecutionDocument Build(
        AlarmRuleDocument rule,
        ObservationEnvelope observation,
        ScenarioGraphExecutionResult execution,
        string trigger,
        DateTime startedAt,
        DateTime finishedAt,
        RuleOutcomeSummary? outputs = null,
        Exception? error = null)
    {
        var outcome = error != null
            ? ScenarioExecutionOutcomes.Error
            : ResolveOutcome(execution);
        return new ScenarioExecutionDocument
        {
            DomainId = observation.DomainId,
            DomainName = observation.DomainName,
            ScenarioId = rule.ScenarioId ?? string.Empty,
            ScenarioVersion = rule.ScenarioVersion,
            RuleId = rule.Id,
            Trigger = trigger,
            Outcome = outcome,
            StartedAt = startedAt,
            FinishedAt = finishedAt,
            DurationMs = Math.Max(0, (long)(finishedAt - startedAt).TotalMilliseconds),
            ObservationKind = observation.Kind,
            ObservationKey = observation.Key,
            ObservationValue = observation.Value,
            AlarmsRaised = outputs?.Raised ?? 0,
            AlarmsUpdated = outputs?.Updated ?? 0,
            OutputNodeIds = execution.Outputs.Select(x => x.OutputNodeId).Distinct(StringComparer.Ordinal).ToList(),
            ErrorCode = error?.GetType().Name,
            ErrorMessage = error == null ? null : Truncate(error.Message, 500),
            NodeTrace = execution.Traces
                .Take(ScenarioExecutionDocument.MaxTraceEntries)
                .Select(x => new ScenarioExecutionTraceEntry
                {
                    NodeId = x.NodeId,
                    NodeType = x.NodeType,
                    Status = x.Status,
                    Outcome = x.Outcome
                })
                .ToList()
        };
    }

    private static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}

public sealed record RuleOutcomeSummary(int Raised, int Updated);
