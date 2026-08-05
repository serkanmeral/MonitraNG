using System.Globalization;
using MongoDB.Bson;
using MngAlarm.Application.Observations;
using MngAlarm.Domain.Constants;
using MngAlarm.Domain.Entities;

namespace MngAlarm.Infrastructure.Evaluation;

public static class ScenarioCompiler
{
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
            if (definition.SchemaVersion != 2)
                Add("schema.unsupported", "Only ScenarioDefinition schemaVersion 2 is supported.", "schemaVersion");
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

        return new ScenarioValidationSnapshot
        {
            IsValid = diagnostics.All(x => !string.Equals(x.Severity, "error", StringComparison.Ordinal)),
            Diagnostics = diagnostics,
            ValidatedAt = DateTime.UtcNow
        };

        void Add(string code, string message, string path) =>
            diagnostics.Add(new ScenarioDiagnostic { Code = code, Message = message, Path = path });
    }

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
        string.Equals(source.MatchKey, observation.Key, StringComparison.Ordinal)
        && (string.IsNullOrWhiteSpace(source.ObservationKind)
            || string.Equals(source.ObservationKind, observation.Kind, StringComparison.OrdinalIgnoreCase))
        && (source.Kind != ScenarioSourceKinds.MetaCorrelation || MetaDepthAllowed(source, observation));

    private static bool MetaDepthAllowed(ScenarioSource source, ObservationEnvelope observation)
    {
        if (observation.Dimensions.TryGetValue("scenarioChainDepth", out var depthValue)
            && int.TryParse(depthValue?.ToString(), out var depth)
            && depth >= source.MaxChainDepth)
            return false;

        return true;
    }

    private static object? ResolveField(string field, ObservationEnvelope observation)
    {
        if (field == "value") return observation.Value;
        if (field == "key") return observation.Key;
        if (field == "kind") return observation.Kind;
        var dimension = field.StartsWith("dimensions.", StringComparison.Ordinal) ? field[11..] : field;
        return observation.Dimensions.TryGetValue(dimension, out var value) ? value : null;
    }

    private static bool Compare(object? actual, object? expected, string op)
    {
        actual = Unwrap(actual);
        expected = Unwrap(expected);
        if (op.Equals("exists", StringComparison.OrdinalIgnoreCase))
            return actual != null == (!bool.TryParse(expected?.ToString(), out var required) || required);
        if (op.Equals("eq", StringComparison.OrdinalIgnoreCase)) return EqualsNormalized(actual, expected);
        if (op.Equals("neq", StringComparison.OrdinalIgnoreCase)) return !EqualsNormalized(actual, expected);
        if (op.Equals("in", StringComparison.OrdinalIgnoreCase) && expected is IEnumerable<object> items)
            return items.Any(item => EqualsNormalized(actual, item));

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

    private static object? Unwrap(object? value) => value switch
    {
        BsonValue bson when bson.IsBsonNull => null,
        BsonValue bson => BsonTypeMapper.MapToDotNetValue(bson),
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
