using MngAlarm.Application.Observations;
using MngAlarm.Domain.Constants;
using MngAlarm.Domain.Entities;
using MngAlarm.Infrastructure.Evaluation;
using MngAlarm.Infrastructure.State;

namespace MngAlarm.Tests.Evaluation;

public sealed class ScenarioCompilerTests
{
    [Fact]
    public void Compiles_legacy_threshold_without_mutating_it()
    {
        var legacy = new AlarmRuleDocument
        {
            Type = AlarmRuleTypes.Threshold,
            MatchKey = "cpu",
            Operator = "gte",
            Threshold = 90,
            CooldownMinutes = 7
        };

        var compiled = ScenarioCompiler.Compile(legacy);

        Assert.Equal(2, compiled.SchemaVersion);
        Assert.Equal("cpu", compiled.Source.MatchKey);
        Assert.Equal("value", compiled.Condition?.Field);
        Assert.Equal(420, compiled.Dedup.CooldownSeconds);
        Assert.Null(legacy.Definition);
    }

    [Fact]
    public void Applies_canonical_sequence_to_legacy_projection()
    {
        var rule = new AlarmRuleDocument();
        var definition = ValidDefinition();
        definition.Sequence = new ScenarioSequence
        {
            Steps =
            [
                new() { MatchKey = "a", MinCount = 2, WithinSeconds = 120 },
                new() { MatchKey = "b", WithinSeconds = 180 },
                new() { MatchKey = "c", WithinSeconds = 240 }
            ]
        };

        ScenarioCompiler.ApplyToLegacyFields(rule, definition);

        Assert.Equal(AlarmRuleTypes.Sequence, rule.Type);
        Assert.Equal(3, rule.SequenceSteps.Count);
        Assert.Equal(4, rule.SequenceSteps[2].WithinMinutes);
    }

    [Fact]
    public void Evaluates_nested_and_or_not_and_dimension_comparisons()
    {
        var condition = new ScenarioCondition
        {
            Logic = "AND",
            Children =
            [
                new() { Field = "dimensions.srcIp", Operator = "startsWith", Value = "10." },
                new()
                {
                    Logic = "NOT",
                    Children = [new() { Field = "dimensions.user", Operator = "eq", Value = "service" }]
                },
                new()
                {
                    Logic = "OR",
                    Children =
                    [
                        new() { Field = "value", Operator = "gte", Value = 5 },
                        new() { Field = "kind", Operator = "eq", Value = "security" }
                    ]
                }
            ]
        };
        var observation = new ObservationEnvelope
        {
            Kind = "event",
            Key = "login",
            Value = 7,
            Dimensions = new() { ["srcIp"] = "10.0.0.4", ["user"] = "admin" }
        };

        Assert.True(ScenarioCompiler.Matches(condition, observation, out var explanation));
        Assert.Contains("AND", explanation);
    }

    [Fact]
    public void Validation_rejects_invalid_group_sequence_and_ranges()
    {
        var definition = ValidDefinition();
        definition.GroupBy = ["user", "user", ""];
        definition.Window = new ScenarioWindow { DurationSeconds = 0 };
        definition.Sequence = new ScenarioSequence
        {
            Steps = [new() { MatchKey = "", MinCount = 0, WithinSeconds = 0 }]
        };

        var validation = ScenarioCompiler.Validate(definition, false);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Diagnostics, x => x.Code == "groupBy.invalid");
        Assert.Contains(validation.Diagnostics, x => x.Code == "sequence.steps.range");
        Assert.Contains(validation.Diagnostics, x => x.Code == "window.range");
    }

    [Fact]
    public void Validation_rejects_freeform_scheduled_query()
    {
        var definition = ValidDefinition();
        definition.Source.Kind = ScenarioSourceKinds.ScheduledQuery;
        definition.Source.Query = "db.events.find({})";
        definition.Source.ScheduleDefinition = new ScenarioSchedule { Expression = "0 * * * *" };

        var validation = ScenarioCompiler.Validate(definition, true);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Diagnostics, x => x.Code == "scheduled.freeform.forbidden");
    }

    [Fact]
    public void Validation_accepts_declarative_scheduled_query_model()
    {
        var definition = ValidDefinition();
        definition.Source.Kind = ScenarioSourceKinds.ScheduledQuery;
        definition.Source.ScheduleDefinition = new ScenarioSchedule
        {
            Expression = "0 * * * *",
            TimeZone = "UTC",
            MaxLookbackSeconds = 3600
        };

        Assert.True(ScenarioCompiler.Validate(definition, true).IsValid);
    }

    [Fact]
    public void Validation_allows_scheduled_staleness()
    {
        var definition = ValidDefinition();
        definition.Source.Kind = ScenarioSourceKinds.ScheduledStaleness;
        definition.Window = new ScenarioWindow { DurationSeconds = 300, StalenessSeconds = 600 };

        Assert.True(ScenarioCompiler.Validate(definition, true).IsValid);
    }

    [Fact]
    public void Nested_sustained_condition_is_deterministic_and_resets()
    {
        var rule = new AlarmRuleDocument { Id = "r1" };
        var condition = new ScenarioCondition
        {
            Logic = "AND",
            Children =
            [
                new() { Field = "dimensions.user", Operator = "eq", Value = "admin", SustainedForSeconds = 10 },
                new() { Field = "value", Operator = "gte", Value = 5 }
            ]
        };
        var states = new InMemorySequenceStateStore();
        var start = DateTime.UtcNow;
        ObservationEnvelope At(int seconds, string user = "admin") => new()
        {
            DomainName = "tenant",
            Value = 7,
            Timestamp = start.AddSeconds(seconds),
            Dimensions = new() { ["user"] = user }
        };

        Assert.False(StatefulScenarioConditionEvaluator.Matches(rule, condition, At(0), states));
        Assert.True(StatefulScenarioConditionEvaluator.Matches(rule, condition, At(10), states));
        Assert.False(StatefulScenarioConditionEvaluator.Matches(rule, condition, At(11, "guest"), states));
        Assert.False(StatefulScenarioConditionEvaluator.Matches(rule, condition, At(15), states));
        Assert.True(StatefulScenarioConditionEvaluator.Matches(rule, condition, At(25), states));
    }

    [Fact]
    public void Hysteresis_uses_raise_clear_and_minimum_duration()
    {
        var hysteresis = new ScenarioHysteresis
        {
            RaiseThreshold = 90,
            ClearThreshold = 80,
            MinimumStateSeconds = 30
        };
        var opened = DateTime.UtcNow;

        Assert.False(StatefulScenarioConditionEvaluator.ApplyHysteresis(hysteresis, 89, true, null, opened));
        Assert.True(StatefulScenarioConditionEvaluator.ApplyHysteresis(hysteresis, 95, true, null, opened));
        Assert.True(StatefulScenarioConditionEvaluator.ApplyHysteresis(hysteresis, 70, false, opened, opened.AddSeconds(10)));
        Assert.False(StatefulScenarioConditionEvaluator.ApplyHysteresis(hysteresis, 70, false, opened, opened.AddSeconds(30)));
    }

    [Fact]
    public void Meta_runtime_rejects_max_depth()
    {
        var source = new ScenarioSource
        {
            Kind = ScenarioSourceKinds.MetaCorrelation,
            MatchKey = "alarm.raised",
            MaxChainDepth = 3,
            DependsOnScenarioIds = ["base"]
        };
        var observation = new ObservationEnvelope
        {
            Key = "alarm.raised",
            Dimensions = new() { ["scenarioChainDepth"] = 3 }
        };

        Assert.False(ScenarioCompiler.SourceMatches(source, observation));
    }

    private static ScenarioDefinition ValidDefinition() => new()
    {
        Source = new ScenarioSource { Kind = ScenarioSourceKinds.Observation, MatchKey = "login" },
        Condition = new ScenarioCondition { Field = "value", Operator = "gte", Value = 1 },
        Window = new ScenarioWindow { DurationSeconds = 300 },
        Dedup = new ScenarioDedup { KeyTemplate = "{ruleId}:{groupKey}", CooldownSeconds = 60 }
    };
}
