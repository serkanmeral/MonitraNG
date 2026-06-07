using MngAlarm.Application.Notifications;
using MngAlarm.Application.Observations;
using MngAlarm.Domain.Constants;
using MngAlarm.Domain.Entities;

namespace MngAlarm.Tests;

public sealed class AlarmNotificationPolicyMatcherTests
{
    [Fact]
    public void Matches_WhenRuleAndSeverityFit()
    {
        var policy = new AlarmNotificationPolicyDocument
        {
            IsActive = true,
            DomainId = "dom-1",
            EventType = AlarmNotificationEventTypes.Raised,
            RuleId = "rule-a",
            MinSeverity = 5,
            MaxSeverity = 9,
        };

        var message = new AlarmEventMessage
        {
            DomainId = "dom-1",
            DomainName = "odak",
            EventType = AlarmNotificationEventTypes.Raised,
            AlarmId = "alarm-1",
            RuleId = "rule-a",
            Severity = 7,
            DedupKey = "k",
            CorrelationId = "c",
            EventId = "e",
        };

        Assert.True(AlarmNotificationPolicyMatcher.Matches(policy, message));
        Assert.Equal(6, AlarmNotificationPolicyMatcher.SpecificityScore(policy));
    }

    [Fact]
    public void Matches_FailsWhenSeverityBelowMin()
    {
        var policy = new AlarmNotificationPolicyDocument
        {
            IsActive = true,
            DomainId = "dom-1",
            EventType = AlarmNotificationEventTypes.Raised,
            MinSeverity = 8,
        };

        var message = new AlarmEventMessage
        {
            DomainId = "dom-1",
            DomainName = "odak",
            EventType = AlarmNotificationEventTypes.Raised,
            AlarmId = "alarm-1",
            RuleId = "rule-a",
            Severity = 5,
            DedupKey = "k",
            CorrelationId = "c",
            EventId = "e",
        };

        Assert.False(AlarmNotificationPolicyMatcher.Matches(policy, message));
    }
}
