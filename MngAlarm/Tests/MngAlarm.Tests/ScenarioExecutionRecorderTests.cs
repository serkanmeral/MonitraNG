using MngAlarm.Application.Observations;
using MngAlarm.Domain.Entities;
using MngAlarm.Infrastructure.Evaluation;

namespace MngAlarm.Tests.Evaluation;

public sealed class ScenarioExecutionRecorderTests
{
    [Fact]
    public void Skipped_source_is_not_persisted_for_observation_trigger()
    {
        var execution = new ScenarioGraphExecutionResult(
            [],
            [new ScenarioNodeTrace("source", ScenarioNodeTypes.Source, "skipped", null, null)],
            [],
            null,
            []);

        Assert.False(ScenarioExecutionRecorder.ShouldPersist(ScenarioExecutionTriggers.Observation, execution));
        Assert.True(ScenarioExecutionRecorder.ShouldPersist(ScenarioExecutionTriggers.Due, execution));
    }

    [Fact]
    public void Outcome_prefers_matched_then_stopped_then_pending()
    {
        var matched = new ScenarioGraphExecutionResult(
            [new ScenarioOutputMatch("alarm", 5, "d", 60, "_all")],
            [new ScenarioNodeTrace("source", ScenarioNodeTypes.Source, "true", true, null)],
            [],
            null,
            []);
        Assert.Equal(ScenarioExecutionOutcomes.Matched, ScenarioExecutionRecorder.ResolveOutcome(matched));

        var stopped = new ScenarioGraphExecutionResult(
            [],
            [
                new ScenarioNodeTrace("source", ScenarioNodeTypes.Source, "true", true, null),
                new ScenarioNodeTrace("stop", ScenarioNodeTypes.StopOutput, "stopped", null, null)
            ],
            [],
            null,
            []);
        Assert.Equal(ScenarioExecutionOutcomes.Stopped, ScenarioExecutionRecorder.ResolveOutcome(stopped));

        var pending = new ScenarioGraphExecutionResult(
            [],
            [
                new ScenarioNodeTrace("source", ScenarioNodeTypes.Source, "true", true, null),
                new ScenarioNodeTrace("threshold", ScenarioNodeTypes.Threshold, "pending", null, DateTime.UtcNow)
            ],
            [new ScenarioPendingEvaluation("threshold", ScenarioNodeTypes.Threshold, "_all", DateTime.UtcNow)],
            DateTime.UtcNow,
            []);
        Assert.Equal(ScenarioExecutionOutcomes.Pending, ScenarioExecutionRecorder.ResolveOutcome(pending));
    }

    [Fact]
    public void Build_error_sets_error_outcome()
    {
        var rule = new AlarmRuleDocument
        {
            Id = "rule-1",
            ScenarioId = "sc-1",
            ScenarioVersion = 2
        };
        var observation = new ObservationEnvelope
        {
            DomainId = "d",
            DomainName = "test",
            Kind = "event",
            Key = "login",
            Timestamp = DateTime.UtcNow
        };
        var empty = new ScenarioGraphExecutionResult([], [], [], null, []);
        var doc = ScenarioExecutionRecorder.Build(
            rule,
            observation,
            empty,
            ScenarioExecutionTriggers.Observation,
            DateTime.UtcNow.AddMilliseconds(-5),
            DateTime.UtcNow,
            null,
            new InvalidOperationException("boom"));

        Assert.Equal(ScenarioExecutionOutcomes.Error, doc.Outcome);
        Assert.Equal("InvalidOperationException", doc.ErrorCode);
        Assert.Equal("boom", doc.ErrorMessage);
        Assert.True(doc.DurationMs >= 0);
    }
}
