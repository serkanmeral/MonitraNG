using MngAlarm.Domain.Entities;
using MngAlarm.Infrastructure.Evaluation;
using MngAlarm.Domain.Enums;
using Xunit;

namespace MngAlarm.Tests.Evaluation;

public sealed class ThresholdEvaluatorTests
{
    [Theory]
    [InlineData(95, "gt", 90, true)]
    [InlineData(90, "gt", 90, false)]
    [InlineData(85, "gte", 85, true)]
    [InlineData(10, "lt", 20, true)]
    public void Evaluates_threshold(double value, string op, double threshold, bool expected)
    {
        var rule = new AlarmRuleDocument { Type = "threshold", Operator = op, Threshold = threshold };
        Assert.Equal(expected, ThresholdEvaluator.Matches(rule, value));
    }

    [Fact]
    public void Builds_dedup_key_from_template()
    {
        var rule = new AlarmRuleDocument { Id = "r1", DedupKeyTemplate = "{ruleId}:{key}" };
        Assert.Equal("r1:cpu_usage", ThresholdEvaluator.BuildDedupKey(rule, "cpu_usage"));
    }

    [Theory]
    [InlineData(AlarmStatus.Active, true)]
    [InlineData(AlarmStatus.Acknowledged, true)]
    [InlineData(AlarmStatus.Suppressed, true)]
    [InlineData(AlarmStatus.Resolved, false)]
    public void Dedup_open_status_semantics_are_explicit(AlarmStatus status, bool expected) =>
        Assert.Equal(expected, status.IsOpen());
}
