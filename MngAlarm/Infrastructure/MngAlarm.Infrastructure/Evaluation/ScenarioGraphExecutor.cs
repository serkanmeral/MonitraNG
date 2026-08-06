using MngAlarm.Application.Observations;
using MngAlarm.Domain.Entities;
using MngAlarm.Infrastructure.State;

namespace MngAlarm.Infrastructure.Evaluation;

public sealed class ScenarioGraphExecutor(
    ICorrelationWindowStore windows,
    ISequenceStateStore states)
{
    public ScenarioGraphExecutionResult Execute(
        AlarmRuleDocument rule,
        ObservationEnvelope observation) =>
        ExecuteCore(rule, observation, null);

    public ScenarioGraphExecutionResult ExecuteDue(
        AlarmRuleDocument rule,
        ObservationEnvelope observation,
        string settledNodeId) =>
        ExecuteCore(rule, observation, settledNodeId);

    private ScenarioGraphExecutionResult ExecuteCore(
        AlarmRuleDocument rule,
        ObservationEnvelope observation,
        string? settledNodeId)
    {
        var plan = ScenarioCompiler.CompileGraph(rule.Definition!);
        var reached = new HashSet<string>(StringComparer.Ordinal);
        var traces = new List<ScenarioNodeTrace>();
        var outputs = new List<ScenarioOutputMatch>();
        var debugLines = new List<ScenarioDebugHit>();
        var pending = new List<ScenarioPendingEvaluation>();
        DateTime? nextEvaluationAt = null;
        if (settledNodeId != null)
        {
            if (!plan.Nodes.ContainsKey(settledNodeId))
                return new ScenarioGraphExecutionResult(outputs, traces, pending, null, debugLines);
            var settledNode = plan.Nodes[settledNodeId];
            var settledGroup = BuildGroupKey(settledNode.Config.GroupBy, observation);
            states.Reset($"{observation.DomainName}:{rule.Id}:v3:{settledNode.Id}:{settledGroup}");
            foreach (var edge in plan.Outgoing.GetValueOrDefault(settledNodeId) ?? [])
                if (edge.FromPort == "false") reached.Add(edge.To);
        }

        foreach (var nodeId in plan.TopologicalOrder)
        {
            var node = plan.Nodes[nodeId];
            if (nodeId == settledNodeId)
            {
                traces.Add(new(nodeId, node.Type, "false", false, null));
                continue;
            }
            var incoming = settledNodeId == null && node.Type == ScenarioNodeTypes.Source
                ? node.Config.Source != null && ScenarioCompiler.SourceMatches(node.Config.Source, observation)
                : reached.Contains(nodeId);
            if (!incoming)
            {
                traces.Add(new(nodeId, node.Type, "skipped", null, null));
                continue;
            }

            DateTime? nodeNextEvaluationAt = null;
            bool? outcome = node.Type switch
            {
                ScenarioNodeTypes.Source => true,
                ScenarioNodeTypes.Condition or ScenarioNodeTypes.Filter or ScenarioNodeTypes.Decision =>
                    ScenarioCompiler.Matches(node.Config.Condition, observation, out _),
                ScenarioNodeTypes.Aggregation or ScenarioNodeTypes.Threshold =>
                    EvaluateThreshold(rule, node, observation, ref nodeNextEvaluationAt),
                ScenarioNodeTypes.Sequence =>
                    EvaluateSequence(rule, node, observation, ref nodeNextEvaluationAt),
                ScenarioNodeTypes.AlarmOutput => null,
                ScenarioNodeTypes.StopOutput => null,
                ScenarioNodeTypes.DebugOutput => null,
                _ => false
            };

            if (node.Type == ScenarioNodeTypes.AlarmOutput)
            {
                var groupKey = BuildGroupKey(node.Config.GroupBy, observation);
                var dedup = node.Config.Dedup!;
                outputs.Add(new ScenarioOutputMatch(
                    node.Id,
                    node.Config.Severity ?? rule.Severity,
                    BuildDedupKey(dedup.KeyTemplate, rule, observation, groupKey, node.Id),
                    dedup.CooldownSeconds,
                    groupKey));
                traces.Add(new(nodeId, node.Type, "matched", null, null));
                continue;
            }
            if (node.Type == ScenarioNodeTypes.StopOutput)
            {
                traces.Add(new(nodeId, node.Type, "stopped", null, null));
                continue;
            }
            if (node.Type == ScenarioNodeTypes.DebugOutput)
            {
                var debug = node.Config.Debug ?? new ScenarioDebug();
                if (!debug.Active)
                {
                    traces.Add(new(nodeId, node.Type, "inactive", null, null));
                    continue;
                }

                var mode = string.IsNullOrWhiteSpace(debug.Mode) ? "complete" : debug.Mode.Trim().ToLowerInvariant();
                var path = string.IsNullOrWhiteSpace(debug.Path) ? null : debug.Path.Trim();
                object? payload = mode == "path"
                    ? ScenarioCompiler.ResolveObservationField(path ?? string.Empty, observation)
                    : BuildObservationSummary(observation);
                debugLines.Add(new ScenarioDebugHit(node.Id, mode, path, payload, observation.Timestamp));
                traces.Add(new(nodeId, node.Type, "debug", null, null));
                continue;
            }

            var port = outcome == true ? "true" : "false";
            if (node.Type == ScenarioNodeTypes.Source) port = "next";
            if (outcome == null)
            {
                nextEvaluationAt = Min(nextEvaluationAt, nodeNextEvaluationAt);
                if (nodeNextEvaluationAt.HasValue)
                    pending.Add(new ScenarioPendingEvaluation(
                        node.Id,
                        node.Type,
                        BuildGroupKey(node.Config.GroupBy, observation),
                        nodeNextEvaluationAt.Value));
                traces.Add(new(nodeId, node.Type, "pending", null, nodeNextEvaluationAt));
                continue;
            }
            foreach (var edge in plan.Outgoing.GetValueOrDefault(nodeId) ?? [])
                if (edge.FromPort == port) reached.Add(edge.To);
            traces.Add(new(nodeId, node.Type, outcome.Value ? "true" : "false", outcome, nodeNextEvaluationAt));
        }

        return new ScenarioGraphExecutionResult(outputs, traces, pending, nextEvaluationAt, debugLines);
    }

    private static Dictionary<string, object?> BuildObservationSummary(ObservationEnvelope observation) =>
        new(StringComparer.Ordinal)
        {
            ["kind"] = observation.Kind,
            ["key"] = observation.Key,
            ["value"] = observation.Value,
            ["timestamp"] = observation.Timestamp,
            ["dimensions"] = observation.Dimensions,
        };

    private bool? EvaluateThreshold(
        AlarmRuleDocument rule,
        ScenarioPlanNode node,
        ObservationEnvelope observation,
        ref DateTime? nextEvaluationAt)
    {
        var aggregation = node.Config.Aggregation!;
        var group = BuildGroupKey(node.Config.GroupBy, observation);
        var key = $"{observation.DomainName}:{rule.Id}:v3:{node.Id}:{group}";
        var duration = TimeSpan.FromSeconds(node.Config.Window?.DurationSeconds ?? 300);
        var count = windows.RecordAndCount(key, observation.Timestamp, duration);
        var matched = Compare(count, aggregation.Operator, aggregation.Threshold);
        if (matched || node.Config.SettleAfterSeconds <= 0)
        {
            states.Reset(key);
            return matched;
        }

        var state = states.GetOrCreate(key);
        state.NextEvaluationAt ??= observation.Timestamp.AddSeconds(node.Config.SettleAfterSeconds);
        if (observation.Timestamp < state.NextEvaluationAt)
        {
            nextEvaluationAt = Min(nextEvaluationAt, state.NextEvaluationAt);
            states.Save(key, state);
            return null;
        }
        states.Reset(key);
        return false;
    }

    private bool? EvaluateSequence(
        AlarmRuleDocument rule,
        ScenarioPlanNode node,
        ObservationEnvelope observation,
        ref DateTime? nextEvaluationAt)
    {
        var steps = node.Config.Sequence!.Steps;
        var group = BuildGroupKey(node.Config.GroupBy, observation);
        var key = $"{observation.DomainName}:{rule.Id}:v3:{node.Id}:{group}";
        var state = states.GetOrCreate(key);
        var stepIndex = Math.Clamp(state.NextStepIndex, 0, steps.Count - 1);
        var step = steps[stepIndex];
        if (state.LastStepTime.HasValue
            && observation.Timestamp > state.LastStepTime.Value.AddSeconds(step.WithinSeconds))
        {
            states.Reset(key);
            return false;
        }
        if (!string.Equals(step.MatchKey, observation.Key, StringComparison.Ordinal)
            || !ScenarioCompiler.Matches(step.Condition, observation, out _))
        {
            if (state.LastStepTime.HasValue)
            {
                state.NextEvaluationAt = state.LastStepTime.Value.AddSeconds(step.WithinSeconds);
                nextEvaluationAt = Min(nextEvaluationAt, state.NextEvaluationAt);
                states.Save(key, state);
                return null;
            }
            return false;
        }

        state.AnchorTime ??= observation.Timestamp;
        state.LastStepTime = observation.Timestamp;
        state.CurrentStepCount++;
        if (state.CurrentStepCount >= step.MinCount)
        {
            state.NextStepIndex++;
            state.CurrentStepCount = 0;
        }
        if (state.NextStepIndex >= steps.Count)
        {
            states.Reset(key);
            return true;
        }
        state.NextEvaluationAt = observation.Timestamp.AddSeconds(
            steps[state.NextStepIndex].WithinSeconds);
        nextEvaluationAt = Min(nextEvaluationAt, state.NextEvaluationAt);
        states.Save(key, state);
        return null;
    }

    private static DateTime? Min(DateTime? left, DateTime? right) =>
        left == null ? right : right == null || left <= right ? left : right;

    private static bool Compare(double value, string operation, double threshold) =>
        operation.ToLowerInvariant() switch
        {
            "gt" => value > threshold,
            "gte" => value >= threshold,
            "lt" => value < threshold,
            "lte" => value <= threshold,
            "eq" => Math.Abs(value - threshold) < double.Epsilon,
            "neq" => Math.Abs(value - threshold) >= double.Epsilon,
            _ => false
        };

    private static string BuildGroupKey(IReadOnlyList<string> fields, ObservationEnvelope observation)
    {
        if (fields.Count == 0) return "_all";
        return string.Join("|", fields.Select(field =>
            observation.Dimensions.TryGetValue(field, out var value) ? value?.ToString() ?? "_null" : "_missing"));
    }

    private static string BuildDedupKey(
        string template,
        AlarmRuleDocument rule,
        ObservationEnvelope observation,
        string groupKey,
        string outputNodeId) =>
        template.Replace("{ruleId}", rule.Id, StringComparison.Ordinal)
            .Replace("{scenarioId}", rule.ScenarioId ?? rule.Id, StringComparison.Ordinal)
            .Replace("{key}", observation.Key, StringComparison.Ordinal)
            .Replace("{groupKey}", groupKey, StringComparison.Ordinal)
            .Replace("{outputNodeId}", outputNodeId, StringComparison.Ordinal);
}

public sealed record ScenarioGraphExecutionResult(
    IReadOnlyList<ScenarioOutputMatch> Outputs,
    IReadOnlyList<ScenarioNodeTrace> Traces,
    IReadOnlyList<ScenarioPendingEvaluation> PendingEvaluations,
    DateTime? NextEvaluationAt,
    IReadOnlyList<ScenarioDebugHit> DebugLines);

public sealed record ScenarioDebugHit(
    string NodeId,
    string Mode,
    string? Path,
    object? Payload,
    DateTime At);

public sealed record ScenarioPendingEvaluation(
    string NodeId,
    string NodeType,
    string GroupKey,
    DateTime NextEvaluationAt);

public sealed record ScenarioOutputMatch(
    string OutputNodeId,
    int Severity,
    string DedupKey,
    int CooldownSeconds,
    string GroupKey);

public sealed record ScenarioNodeTrace(
    string NodeId,
    string NodeType,
    string Status,
    bool? Outcome,
    DateTime? NextEvaluationAt);
