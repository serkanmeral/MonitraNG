using MngAlarm.Application.Observations;
using MngAlarm.Domain.Entities;

namespace MngAlarm.Application.Notifications;

public static class AlarmNotificationPolicyMatcher
{
    public static bool Matches(AlarmNotificationPolicyDocument policy, AlarmEventMessage message)
    {
        if (!policy.IsActive)
            return false;

        if (!string.Equals(policy.DomainId, message.DomainId, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.Equals(policy.EventType, message.EventType, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(policy.RuleId)
            && !string.Equals(policy.RuleId, message.RuleId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (policy.MinSeverity.HasValue && message.Severity < policy.MinSeverity.Value)
            return false;

        if (policy.MaxSeverity.HasValue && message.Severity > policy.MaxSeverity.Value)
            return false;

        return true;
    }

    public static int SpecificityScore(AlarmNotificationPolicyDocument policy)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(policy.RuleId))
            score += 4;
        if (policy.MinSeverity.HasValue || policy.MaxSeverity.HasValue)
            score += 2;
        return score;
    }
}
