using MngAlarm.Application.Observations;
using MngAlarm.Domain.Entities;
using MngAlarm.Infrastructure.Evaluation;
using Xunit;

namespace MngAlarm.Tests.Evaluation;

public sealed class CorrelationEvaluatorTests
{
    [Fact]
    public void Counts_events_in_group()
    {
        var rule = new AlarmRuleDocument
        {
            Id = "r1",
            Type = "correlation",
            MatchKey = "auth_failure",
            GroupByFields = ["userId", "srcIp"],
            Threshold = 3,
            DedupKeyTemplate = "{ruleId}:{groupKey}"
        };

        var obs = new ObservationEnvelope
        {
            DomainId = "d1",
            DomainName = "odak",
            Key = "auth_failure",
            Dimensions = new Dictionary<string, object?> { ["userId"] = "u1", ["srcIp"] = "1.2.3.4" }
        };

        Assert.True(CorrelationEvaluator.MatchesEvent(rule, obs));
        Assert.Equal("u1|1.2.3.4", CorrelationEvaluator.BuildGroupKey(rule, obs.Dimensions));
        Assert.Equal("r1:u1|1.2.3.4", CorrelationEvaluator.BuildDedupKey(rule, "u1|1.2.3.4"));
        Assert.True(CorrelationEvaluator.IsBreaching(3, rule.Threshold));
        Assert.False(CorrelationEvaluator.IsBreaching(2, rule.Threshold));
    }
}
