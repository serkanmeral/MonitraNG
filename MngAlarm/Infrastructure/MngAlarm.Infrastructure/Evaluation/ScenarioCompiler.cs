using System.Collections;
using System.Globalization;
using MongoDB.Bson;
using MngAlarm.Application.Observations;
using MngAlarm.Domain.Constants;
using MngAlarm.Domain.Entities;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace MngAlarm.Infrastructure.Evaluation;

public static class ScenarioCompiler
{
    public const int MaxGraphNodes = 100;
    public const int MaxGraphEdges = 300;

    private static readonly HashSet<string> ComparisonOperators =
        new(["eq", "neq", "gt", "gte", "lt", "lte", "contains", "startsWith", "endsWith", "exists", "in"],
            StringComparer.OrdinalIgnoreCase);

    public static ScenarioDefinition Compile(AlarmRuleDocument rule)
    {
        if (rule.Definition != null)
            return rule.Definition;

        var definition = new ScenarioDefinition
        {
            Source = new ScenarioSource
            {
                Kind = string.Equals(rule.Type, AlarmRuleTypes.Scheduled, StringComparison.Ordinal)
                    ? ScenarioSourceKinds.ScheduledStaleness
                    : ScenarioSourceKinds.Observation,
                MatchKey = rule.MatchKey
            },
            Condition = string.Equals(rule.Type, AlarmRuleTypes.Threshold, StringComparison.Ordinal)
                ? new ScenarioCondition { Field = "value", Operator = rule.Operator, Value = rule.Threshold }
                : null,
            Aggregation = string.Equals(rule.Type, AlarmRuleTypes.Correlation, StringComparison.Ordinal)
                ? new ScenarioAggregation { Function = "count", Operator = rule.Operator, Threshold = rule.Threshold }
                : null,
            GroupBy = [.. rule.GroupByFields],
            Window = new ScenarioWindow
            {
                DurationSeconds = Math.Max(1, rule.WindowMinutes) * 60,
                StalenessSeconds = Math.Max(0, rule.StalenessMinutes) * 60
            },
            Dedup = new ScenarioDedup
            {
                KeyTemplate = string.IsNullOrWhiteSpace(rule.DedupKeyTemplate)
                    ? "{ruleId}:{key}"
                    : rule.DedupKeyTemplate,
                CooldownSeconds = Math.Max(0, rule.CooldownMinutes) * 60
            }
        };

        if (rule.SequenceSteps.Count > 0)
        {
            definition.Sequence = new ScenarioSequence
            {
                Steps = rule.SequenceSteps.Select(step => new ScenarioSequenceStep
                {
                    MatchKey = step.MatchKey,
                    MinCount = step.MinCount,
                    WithinSeconds = Math.Max(step.WithinMinutes, step.WithinMinutesAfterFirst) * 60
                }).ToList()
            };
        }

        return definition;
    }

    public static void ApplyToLegacyFields(AlarmRuleDocument rule, ScenarioDefinition definition)
    {
        rule.Definition = definition;
        if (definition.SchemaVersion == 3 && definition.Graph != null)
        {
            var source = definition.Graph.Nodes.First(x => x.Type == ScenarioNodeTypes.Source).Config.Source!;
            var output = definition.Graph.Nodes.First(x => x.Type == ScenarioNodeTypes.AlarmOutput);
            rule.MatchKey = source.MatchKey;
            rule.Type = AlarmRuleTypes.Threshold;
            rule.Severity = output.Config.Severity ?? rule.Severity;
            rule.DedupKeyTemplate = output.Config.Dedup?.KeyTemplate ?? "{ruleId}:{key}:{outputNodeId}";
            rule.CooldownMinutes = Math.Max(0, (int)Math.Ceiling((output.Config.Dedup?.CooldownSeconds ?? 0) / 60d));
            return;
        }
        rule.MatchKey = definition.Source.MatchKey;
        rule.GroupByFields = [.. definition.GroupBy];
        rule.WindowMinutes = Math.Max(1, (int)Math.Ceiling((definition.Window?.DurationSeconds ?? 300) / 60d));
        rule.StalenessMinutes = Math.Max(0, (int)Math.Ceiling((definition.Window?.StalenessSeconds ?? 0) / 60d));
        rule.DedupKeyTemplate = definition.Dedup.KeyTemplate;
        rule.CooldownMinutes = Math.Max(0, (int)Math.Ceiling(definition.Dedup.CooldownSeconds / 60d));

        if (definition.Sequence?.Steps.Count > 0)
        {
            rule.Type = AlarmRuleTypes.Sequence;
            rule.SequenceSteps = definition.Sequence.Steps.Select(step => new AlarmSequenceStep
            {
                MatchKey = step.MatchKey,
                MinCount = step.MinCount,
                WithinMinutes = Math.Max(1, (int)Math.Ceiling(step.WithinSeconds / 60d)),
                WithinMinutesAfterFirst = Math.Max(1, (int)Math.Ceiling(step.WithinSeconds / 60d))
            }).ToList();
        }
        else if (definition.Source.Kind == ScenarioSourceKinds.ScheduledStaleness)
        {
            rule.Type = AlarmRuleTypes.Scheduled;
        }
        else if (definition.Aggregation != null)
        {
            rule.Type = AlarmRuleTypes.Correlation;
            rule.Operator = definition.Aggregation.Operator;
            rule.Threshold = definition.Aggregation.Threshold;
        }
        else
        {
            rule.Type = AlarmRuleTypes.Threshold;
            if (definition.Condition is { Logic: null, Field: "value" } condition)
            {
                rule.Operator = condition.Operator ?? "gt";
                if (TryDouble(condition.Value, out var threshold))
                    rule.Threshold = threshold;
            }
        }
    }

    public static ScenarioValidationSnapshot Validate(ScenarioDefinition? definition, bool enabled)
    {
        var diagnostics = new List<ScenarioDiagnostic>();
        if (definition == null)
            Add("definition.required", "Definition is required.", "definition");
        else
        {
            var supportedSources = new[]
            {
                ScenarioSourceKinds.Observation,
                ScenarioSourceKinds.ScheduledStaleness,
                ScenarioSourceKinds.ScheduledQuery,
                ScenarioSourceKinds.MetaCorrelation
            };
            if (!supportedSources.Contains(definition.Source.Kind, StringComparer.Ordinal))
                Add("source.kind.invalid", "Unsupported source kind.", "source.kind");
            if (definition.SchemaVersion is not (2 or 3))
                Add("schema.unsupported", "Only ScenarioDefinition schemaVersion 2 and 3 are supported.", "schemaVersion");
            if (definition.SchemaVersion == 3)
            {
                ValidateGraph(definition.Graph, diagnostics);
                return BuildValidation(diagnostics);
            }
            if (string.IsNullOrWhiteSpace(definition.Source.MatchKey))
                Add("source.matchKey.required", "Source requires matchKey.", "source.matchKey");
            if (definition.GroupBy.Any(string.IsNullOrWhiteSpace) || definition.GroupBy.Count != definition.GroupBy.Distinct(StringComparer.Ordinal).Count())
                Add("groupBy.invalid", "groupBy fields must be non-empty and unique.", "groupBy");
            if (definition.Window is { DurationSeconds: <= 0 or > 604800 })
                Add("window.range", "Window duration must be between 1 second and 7 days.", "window.durationSeconds");
            if (definition.Source.Kind == ScenarioSourceKinds.ScheduledStaleness
                && (definition.Window?.StalenessSeconds ?? 0) <= 0)
                Add("staleness.required", "Scheduled staleness requires a positive staleness window.", "window.stalenessSeconds");
            if (definition.Source.Kind == ScenarioSourceKinds.ScheduledQuery
                && string.IsNullOrWhiteSpace(definition.Source.ScheduleDefinition?.Expression))
                Add("scheduled.schedule.required", "Scheduled query requires a declarative schedule.", "source.scheduleDefinition");
            if (definition.Source.Kind == ScenarioSourceKinds.ScheduledQuery
                && !string.IsNullOrWhiteSpace(definition.Source.Query))
                Add("scheduled.freeform.forbidden", "Free-form Mongo/SQL query text is forbidden; use condition and aggregation.", "source.query");
            if (definition.Source.ScheduleDefinition is { MaxLookbackSeconds: <= 0 or > 604800 })
                Add("scheduled.lookback.range", "Schedule lookback must be between 1 second and 7 days.", "source.scheduleDefinition.maxLookbackSeconds");
            if (definition.Source.Kind == ScenarioSourceKinds.MetaCorrelation
                && (definition.Source.DependsOnScenarioIds.Count == 0 || definition.Source.MaxChainDepth is < 1 or > 20))
                Add("meta.dependencies.invalid", "Meta-correlation requires dependencies and maxChainDepth between 1 and 20.", "source");
            if ((definition.Source.Query?.Length ?? 0) > 10000 || (definition.Source.Schedule?.Length ?? 0) > 200)
                Add("source.input.tooLong", "Query or schedule exceeds the safe model limit.", "source");
            if (definition.Dedup.CooldownSeconds is < 0 or > 2592000)
                Add("cooldown.range", "Cooldown must be between 0 and 30 days.", "dedup.cooldownSeconds");
            if (string.IsNullOrWhiteSpace(definition.Dedup.KeyTemplate))
                Add("dedup.key.required", "A dedup key template is required.", "dedup.keyTemplate");
            if (definition.Hysteresis is { } hysteresis)
            {
                if (!double.IsFinite(hysteresis.RaiseThreshold)
                    || !double.IsFinite(hysteresis.ClearThreshold)
                    || hysteresis.ClearThreshold >= hysteresis.RaiseThreshold)
                    Add("hysteresis.thresholds.invalid", "clearThreshold must be lower than raiseThreshold.", "hysteresis");
                if (hysteresis.MinimumStateSeconds is < 0 or > 604800)
                    Add("hysteresis.duration.range", "minimumStateSeconds must be between 0 and 7 days.", "hysteresis.minimumStateSeconds");
            }

            ValidateCondition(definition.Condition, "condition", diagnostics);
            if (definition.Aggregation is { } aggregation)
            {
                if (!new[] { "count", "sum", "avg", "min", "max" }.Contains(aggregation.Function, StringComparer.OrdinalIgnoreCase))
                    Add("aggregation.function.invalid", "Unsupported aggregation function.", "aggregation.function");
                if (!new[] { "eq", "neq", "gt", "gte", "lt", "lte" }.Contains(aggregation.Operator, StringComparer.OrdinalIgnoreCase))
                    Add("aggregation.operator.invalid", "Unsupported aggregation operator.", "aggregation.operator");
                if (!string.Equals(aggregation.Function, "count", StringComparison.OrdinalIgnoreCase)
                    && string.IsNullOrWhiteSpace(aggregation.Field))
                    Add("aggregation.field.required", "This aggregation function requires a field.", "aggregation.field");
                if (!double.IsFinite(aggregation.Threshold) || aggregation.Threshold < 0)
                    Add("aggregation.threshold.range", "Aggregation threshold must be finite and non-negative.", "aggregation.threshold");
                if (enabled && !string.Equals(aggregation.Function, "count", StringComparison.OrdinalIgnoreCase))
                    Add("runtime.aggregation.unsupported", "Enabled publish currently supports count aggregation only.", "aggregation.function");
            }

            if (definition.Sequence != null)
            {
                if (definition.Sequence.Steps.Count < 2 || definition.Sequence.Steps.Count > 20)
                    Add("sequence.steps.range", "Sequence requires 2 to 20 steps.", "sequence.steps");
                for (var i = 0; i < definition.Sequence.Steps.Count; i++)
                {
                    var step = definition.Sequence.Steps[i];
                    if (string.IsNullOrWhiteSpace(step.MatchKey))
                        Add("sequence.matchKey.required", "Every sequence step requires matchKey.", $"sequence.steps[{i}].matchKey");
                    if (step.MinCount is < 1 or > 10000)
                        Add("sequence.minCount.range", "Sequence minCount must be between 1 and 10000.", $"sequence.steps[{i}].minCount");
                    if (step.WithinSeconds is <= 0 or > 604800)
                        Add("sequence.window.range", "Sequence step window must be between 1 second and 7 days.", $"sequence.steps[{i}].withinSeconds");
                    ValidateCondition(step.Condition, $"sequence.steps[{i}].condition", diagnostics);
                }
            }

        }

        return BuildValidation(diagnostics);

        static ScenarioValidationSnapshot BuildValidation(List<ScenarioDiagnostic> items) => new()
        {
            IsValid = items.All(x => !string.Equals(x.Severity, "error", StringComparison.Ordinal)),
            Diagnostics = items,
            ValidatedAt = DateTime.UtcNow
        };

        void Add(string code, string message, string path) =>
            diagnostics.Add(new ScenarioDiagnostic { Code = code, Message = message, Path = path });
    }

    public static ScenarioExecutionPlan CompileGraph(ScenarioDefinition definition)
    {
        var validation = Validate(definition, true);
        if (!validation.IsValid)
            throw new ScenarioCompilationException(validation.Diagnostics);

        var graph = definition.Graph!;
        var nodes = graph.Nodes.ToDictionary(
            x => x.Id,
            x => new ScenarioPlanNode(x.Id, x.Type, CloneConfig(x.Config)),
            StringComparer.Ordinal);
        var outgoing = graph.Edges
            .GroupBy(x => x.From, StringComparer.Ordinal)
            .ToDictionary(
                x => x.Key,
                x => (IReadOnlyList<ScenarioPlanEdge>)x
                    .Select(e => new ScenarioPlanEdge(e.Id, e.From, e.To, e.FromPort, e.ToPort))
                    .ToArray(),
                StringComparer.Ordinal);
        var indegree = nodes.Keys.ToDictionary(x => x, _ => 0, StringComparer.Ordinal);
        foreach (var edge in graph.Edges) indegree[edge.To]++;
        var queue = new Queue<string>(indegree.Where(x => x.Value == 0).Select(x => x.Key).Order(StringComparer.Ordinal));
        var order = new List<string>(nodes.Count);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            order.Add(id);
            foreach (var edge in outgoing.GetValueOrDefault(id) ?? [])
                if (--indegree[edge.To] == 0) queue.Enqueue(edge.To);
        }

        var matchKeys = nodes.Values
            .Where(x => x.Type == ScenarioNodeTypes.Source && x.Config.Source != null)
            .SelectMany(x => EffectiveMatchKeys(x.Config.Source!))
            .ToHashSet(StringComparer.Ordinal);
        return new ScenarioExecutionPlan(
            new ReadOnlyDictionary<string, ScenarioPlanNode>(nodes),
            new ReadOnlyDictionary<string, IReadOnlyList<ScenarioPlanEdge>>(outgoing),
            order.AsReadOnly(),
            new ReadOnlySet<string>(matchKeys));
    }

    private static void ValidateGraph(ScenarioGraph? graph, List<ScenarioDiagnostic> diagnostics)
    {
        void Add(string code, string message, string path) =>
            diagnostics.Add(new ScenarioDiagnostic { Code = code, Message = message, Path = path });
        if (graph == null)
        {
            Add("graph.required", "Schema version 3 requires graph.", "graph");
            return;
        }
        if (graph.Nodes.Count is 0 or > MaxGraphNodes)
            Add("graph.nodes.range", $"Graph requires 1 to {MaxGraphNodes} nodes.", "graph.nodes");
        if (graph.Edges.Count > MaxGraphEdges)
            Add("graph.edges.range", $"Graph supports at most {MaxGraphEdges} edges.", "graph.edges");

        var supported = new HashSet<string>([
            ScenarioNodeTypes.Source, ScenarioNodeTypes.Condition, ScenarioNodeTypes.Filter,
            ScenarioNodeTypes.Aggregation, ScenarioNodeTypes.Threshold, ScenarioNodeTypes.Sequence,
            ScenarioNodeTypes.Decision, ScenarioNodeTypes.AlarmOutput, ScenarioNodeTypes.StopOutput,
            ScenarioNodeTypes.DebugOutput
        ], StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (node, index) in graph.Nodes.Select((x, i) => (x, i)))
        {
            var path = $"graph.nodes[{index}]";
            if (string.IsNullOrWhiteSpace(node.Id) || !ids.Add(node.Id))
                Add("graph.node.id.invalid", "Node id must be non-empty and unique.", $"{path}.id");
            if (!supported.Contains(node.Type))
                Add("graph.node.type.invalid", "Unsupported graph node type.", $"{path}.type");
            if (node.Type == ScenarioNodeTypes.Source && string.IsNullOrWhiteSpace(node.Config.Source?.MatchKey))
                Add("graph.source.matchKey.required", "Source node requires matchKey.", $"{path}.config.source.matchKey");
            if (node.Type is ScenarioNodeTypes.Condition or ScenarioNodeTypes.Filter or ScenarioNodeTypes.Decision)
                ValidateCondition(node.Config.Condition, $"{path}.config.condition", diagnostics);
            if (node.Type is ScenarioNodeTypes.Aggregation or ScenarioNodeTypes.Threshold
                && node.Config.Aggregation == null)
                Add("graph.aggregation.required", "Aggregation/threshold node requires aggregation config.", $"{path}.config.aggregation");
            if (node.Type == ScenarioNodeTypes.Sequence && (node.Config.Sequence?.Steps.Count ?? 0) < 2)
                Add("graph.sequence.steps", "Sequence node requires at least two steps.", $"{path}.config.sequence.steps");
            if (node.Type == ScenarioNodeTypes.AlarmOutput)
            {
                if (node.Config.Severity is < 1 or > 10)
                    Add("graph.output.severity.range", "Alarm output severity must be between 1 and 10.", $"{path}.config.severity");
                if (string.IsNullOrWhiteSpace(node.Config.Dedup?.KeyTemplate))
                    Add("graph.output.dedup.required", "Alarm output requires dedup configuration.", $"{path}.config.dedup");
            }
            if (node.Type == ScenarioNodeTypes.DebugOutput)
            {
                var mode = node.Config.Debug?.Mode?.Trim().ToLowerInvariant() ?? "complete";
                if (mode is not ("complete" or "path"))
                    Add("graph.debug.mode.invalid", "Debug mode must be 'complete' or 'path'.", $"{path}.config.debug.mode");
                if (mode == "path" && string.IsNullOrWhiteSpace(node.Config.Debug?.Path))
                    Add("graph.debug.path.required", "Debug path mode requires a field path.", $"{path}.config.debug.path");
            }
            if (node.Config.SettleAfterSeconds is < 0 or > 604800)
                Add("graph.node.settle.range", "settleAfterSeconds must be between 0 and 7 days.", $"{path}.config.settleAfterSeconds");
        }
        if (graph.Nodes.Count(x => x.Type == ScenarioNodeTypes.Source) == 0)
            Add("graph.source.required", "Graph requires at least one source.", "graph.nodes");
        // Debug-output is sim-only and does not satisfy the required real output (alarm/stop).
        if (graph.Nodes.All(x => x.Type is not (ScenarioNodeTypes.AlarmOutput or ScenarioNodeTypes.StopOutput)))
            Add("graph.output.required", "Graph requires at least one output.", "graph.nodes");

        var edgeIds = new HashSet<string>(StringComparer.Ordinal);
        var indegree = ids.ToDictionary(x => x, _ => 0, StringComparer.Ordinal);
        foreach (var (edge, index) in graph.Edges.Select((x, i) => (x, i)))
        {
            var path = $"graph.edges[{index}]";
            if (string.IsNullOrWhiteSpace(edge.Id) || !edgeIds.Add(edge.Id))
                Add("graph.edge.id.invalid", "Edge id must be non-empty and unique.", $"{path}.id");
            if (!ids.Contains(edge.From) || !ids.Contains(edge.To) || edge.From == edge.To)
            {
                Add("graph.edge.endpoint.invalid", "Edge endpoints must reference distinct existing nodes.", path);
                continue;
            }
            if (edge.ToPort != "in")
                Add("graph.edge.toPort.invalid", "Only the 'in' input port is supported.", $"{path}.toPort");
            var fromType = graph.Nodes.First(x => x.Id == edge.From).Type;
            var allowedPorts = fromType is ScenarioNodeTypes.Condition or ScenarioNodeTypes.Filter
                or ScenarioNodeTypes.Decision or ScenarioNodeTypes.Aggregation or ScenarioNodeTypes.Threshold
                or ScenarioNodeTypes.Sequence
                ? new[] { "true", "false" }
                : fromType is ScenarioNodeTypes.AlarmOutput or ScenarioNodeTypes.StopOutput
                    or ScenarioNodeTypes.DebugOutput
                    ? []
                    : ["next"];
            if (!allowedPorts.Contains(edge.FromPort, StringComparer.Ordinal))
                Add("graph.edge.fromPort.invalid", "Edge uses an invalid output port.", $"{path}.fromPort");
            indegree[edge.To]++;
        }
        foreach (var group in graph.Edges.GroupBy(x => (x.From, x.FromPort)))
            if (group.Count() > 10)
                Add("graph.fanout.exceeded", "A port can fan out to at most 10 edges.", "graph.edges");
        foreach (var node in graph.Nodes.Where(x => x.Type != ScenarioNodeTypes.Source && indegree.GetValueOrDefault(x.Id) == 0))
            Add("graph.node.unreachable", "Non-source node must have an incoming edge.", $"graph.nodes.{node.Id}");

        var pending = new Queue<string>(indegree.Where(x => x.Value == 0).Select(x => x.Key));
        var visited = 0;
        while (pending.Count > 0)
        {
            var id = pending.Dequeue();
            visited++;
            foreach (var edge in graph.Edges.Where(x => x.From == id))
                if (--indegree[edge.To] == 0) pending.Enqueue(edge.To);
        }
        if (visited != graph.Nodes.Count)
            Add("graph.cycle", "Graph must be a directed acyclic graph.", "graph.edges");
    }

    private static ScenarioNodeConfig CloneConfig(ScenarioNodeConfig config) =>
        JsonSerializer.Deserialize<ScenarioNodeConfig>(JsonSerializer.Serialize(config))!;

    private static void ValidateCondition(ScenarioCondition? condition, string path, List<ScenarioDiagnostic> diagnostics)
    {
        if (condition == null)
            return;

        if (!string.IsNullOrWhiteSpace(condition.Logic))
        {
            var logic = condition.Logic.ToUpperInvariant();
            if (logic is not ("AND" or "OR" or "NOT"))
                diagnostics.Add(new ScenarioDiagnostic { Code = "condition.logic.invalid", Message = "Logic must be AND, OR or NOT.", Path = path });
            if ((logic == "NOT" && condition.Children.Count != 1) || (logic != "NOT" && condition.Children.Count == 0))
                diagnostics.Add(new ScenarioDiagnostic { Code = "condition.children.invalid", Message = "Logical condition has an invalid child count.", Path = path });
            for (var i = 0; i < condition.Children.Count; i++)
                ValidateCondition(condition.Children[i], $"{path}.children[{i}]", diagnostics);
            return;
        }

        if (string.IsNullOrWhiteSpace(condition.Field) || !ComparisonOperators.Contains(condition.Operator ?? string.Empty))
            diagnostics.Add(new ScenarioDiagnostic { Code = "condition.comparison.invalid", Message = "Comparison requires a field and supported operator.", Path = path });
        if (condition.SustainedForSeconds is < 0 or > 604800)
            diagnostics.Add(new ScenarioDiagnostic { Code = "condition.sustained.range", Message = "sustainedForSeconds must be between 0 and 7 days.", Path = path });
    }

    public static bool Matches(ScenarioCondition? condition, ObservationEnvelope observation, out string explanation)
    {
        if (condition == null)
        {
            explanation = "No condition; source matched.";
            return true;
        }

        if (!string.IsNullOrWhiteSpace(condition.Logic))
        {
            var results = condition.Children.Select(child => Matches(child, observation, out _)).ToList();
            var matched = condition.Logic.ToUpperInvariant() switch
            {
                "AND" => results.All(x => x),
                "OR" => results.Any(x => x),
                "NOT" => results.Count == 1 && !results[0],
                _ => false
            };
            explanation = $"{condition.Logic.ToUpperInvariant()} condition evaluated to {matched}.";
            return matched;
        }

        var actual = ResolveField(condition.Field!, observation);
        var matchedComparison = Compare(actual, condition.Value, condition.Operator!);
        explanation = $"{condition.Field} {condition.Operator} {Format(condition.Value)} evaluated to {matchedComparison} (actual={Format(actual)}).";
        return matchedComparison;
    }

    public static bool SourceMatches(ScenarioSource source, ObservationEnvelope observation) =>
        EffectiveMatchKeys(source).Any(key => string.Equals(key, observation.Key, StringComparison.Ordinal))
        && (string.IsNullOrWhiteSpace(source.ObservationKind)
            || string.Equals(source.ObservationKind, observation.Kind, StringComparison.OrdinalIgnoreCase))
        && (source.Kind != ScenarioSourceKinds.MetaCorrelation || MetaDepthAllowed(source, observation));

    public static IReadOnlyList<string> EffectiveMatchKeys(ScenarioSource source)
    {
        var keys = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void Add(string? key)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            var trimmed = key.Trim();
            if (!seen.Add(trimmed)) return;
            keys.Add(trimmed);
        }

        Add(source.MatchKey);
        if (source.MatchKeys != null)
        {
            foreach (var key in source.MatchKeys)
                Add(key);
        }

        return keys;
    }

    private static bool MetaDepthAllowed(ScenarioSource source, ObservationEnvelope observation)
    {
        if (observation.Dimensions.TryGetValue("scenarioChainDepth", out var depthValue)
            && int.TryParse(depthValue?.ToString(), out var depth)
            && depth >= source.MaxChainDepth)
            return false;

        return true;
    }

    public static object? ResolveObservationField(string field, ObservationEnvelope observation)
    {
        if (string.IsNullOrWhiteSpace(field)) return null;
        var trimmed = field.Trim();
        if (trimmed == "value") return observation.Value;
        if (trimmed == "key") return observation.Key;
        if (trimmed == "kind") return observation.Kind;
        if (trimmed == "timestamp") return observation.Timestamp;
        var dimension = trimmed.StartsWith("dimensions.", StringComparison.Ordinal) ? trimmed[11..] : trimmed;
        return observation.Dimensions.TryGetValue(dimension, out var value) ? value : null;
    }

    private static object? ResolveField(string field, ObservationEnvelope observation) =>
        ResolveObservationField(field, observation);

    private static bool Compare(object? actual, object? expected, string op)
    {
        actual = Unwrap(actual);
        expected = Unwrap(expected);
        if (op.Equals("exists", StringComparison.OrdinalIgnoreCase))
            return actual != null == (!bool.TryParse(expected?.ToString(), out var required) || required);
        if (op.Equals("eq", StringComparison.OrdinalIgnoreCase)) return EqualsNormalized(actual, expected);
        if (op.Equals("neq", StringComparison.OrdinalIgnoreCase)) return !EqualsNormalized(actual, expected);
        if (op.Equals("in", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var item in EnumerateInValues(expected))
            {
                if (EqualsNormalized(actual, item))
                    return true;
            }

            return false;
        }

        if (TryDouble(actual, out var left) && TryDouble(expected, out var right))
            return op.ToLowerInvariant() switch { "gt" => left > right, "gte" => left >= right, "lt" => left < right, "lte" => left <= right, _ => false };

        var actualText = actual?.ToString() ?? string.Empty;
        var expectedText = expected?.ToString() ?? string.Empty;
        return op.ToLowerInvariant() switch
        {
            "contains" => actualText.Contains(expectedText, StringComparison.OrdinalIgnoreCase),
            "startswith" => actualText.StartsWith(expectedText, StringComparison.OrdinalIgnoreCase),
            "endswith" => actualText.EndsWith(expectedText, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static IEnumerable<object?> EnumerateInValues(object? expected)
    {
        expected = Unwrap(expected);
        switch (expected)
        {
            case null:
                yield break;
            case string text:
                foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    yield return part;
                yield break;
            case JsonElement json when json.ValueKind == JsonValueKind.Array:
                foreach (var element in json.EnumerateArray())
                    yield return Unwrap(element);
                yield break;
            case IEnumerable enumerable and not string:
                foreach (var item in enumerable)
                    yield return Unwrap(item);
                yield break;
            default:
                yield return expected;
                break;
        }
    }

    private static object? Unwrap(object? value) => value switch
    {
        BsonValue bson when bson.IsBsonNull => null,
        BsonValue bson => BsonTypeMapper.MapToDotNetValue(bson),
        JsonElement json when json.ValueKind == JsonValueKind.Null => null,
        JsonElement json when json.ValueKind == JsonValueKind.True => true,
        JsonElement json when json.ValueKind == JsonValueKind.False => false,
        JsonElement json when json.ValueKind == JsonValueKind.Number && json.TryGetDouble(out var number) => number,
        JsonElement json when json.ValueKind == JsonValueKind.String => json.GetString(),
        JsonElement json when json.ValueKind == JsonValueKind.Array => json,
        _ => value
    };

    private static bool EqualsNormalized(object? left, object? right) =>
        TryDouble(left, out var l) && TryDouble(right, out var r)
            ? Math.Abs(l - r) < double.Epsilon
            : string.Equals(left?.ToString(), right?.ToString(), StringComparison.OrdinalIgnoreCase);

    private static bool TryDouble(object? value, out double result) =>
        double.TryParse(Unwrap(value)?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out result);

    private static string Format(object? value) => Unwrap(value)?.ToString() ?? "null";
}

public sealed record ScenarioPlanNode(string Id, string Type, ScenarioNodeConfig Config);
public sealed record ScenarioPlanEdge(string Id, string From, string To, string FromPort, string ToPort);
public sealed record ScenarioExecutionPlan(
    IReadOnlyDictionary<string, ScenarioPlanNode> Nodes,
    IReadOnlyDictionary<string, IReadOnlyList<ScenarioPlanEdge>> Outgoing,
    IReadOnlyList<string> TopologicalOrder,
    IReadOnlySet<string> SourceMatchKeys);

public sealed class ScenarioCompilationException(IReadOnlyList<ScenarioDiagnostic> diagnostics)
    : Exception("Scenario graph validation failed.")
{
    public IReadOnlyList<ScenarioDiagnostic> Diagnostics { get; } = diagnostics;
}
