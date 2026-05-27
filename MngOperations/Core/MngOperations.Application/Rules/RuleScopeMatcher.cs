using MngOperations.Application.Models;

namespace MngOperations.Application.Rules;

public static class RuleScopeMatcher
{
    public static bool Matches(RuleRecord rule, RuleExecutionContext context, DateTime utcNow)
    {
        if (rule.IsActive == false)
            return false;

        if (rule.ValidFrom is { } from && utcNow < from)
            return false;

        if (rule.ValidTo is { } to && utcNow > to)
            return false;

        if (!string.IsNullOrEmpty(rule.WorkspaceId)
            && !string.Equals(rule.WorkspaceId, context.WorkspaceId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(rule.TypeId)
            && !string.Equals(rule.TypeId, context.TypeId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(rule.BoardId)
            && !string.Equals(rule.BoardId, context.BoardId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(rule.StateId)
            && !string.Equals(rule.StateId, context.StateId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(rule.FromStateId)
            && !string.Equals(rule.FromStateId, context.FromStateId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(rule.ToStateId)
            && !string.Equals(rule.ToStateId, context.ToStateId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(rule.TransitionKey)
            && !string.Equals(rule.TransitionKey, context.TransitionKey, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    public static bool MatchesTrigger(RuleRecord rule, string trigger) =>
        string.Equals(rule.Trigger, trigger, StringComparison.OrdinalIgnoreCase);

    public static bool MatchesPhase(RuleRecord rule, RulePhase phase)
    {
        var ruleType = rule.RuleType?.Trim().ToLowerInvariant();
        var applyMode = rule.ApplyMode?.Trim().ToLowerInvariant();

        return phase switch
        {
            RulePhase.PreValidation => ruleType == RuleTypes.Validation
                && (string.IsNullOrEmpty(applyMode)
                    || applyMode is "pre" or "prevalidation" or "before"),
            RulePhase.PostValidation => ruleType == RuleTypes.Validation
                && applyMode is "post" or "postvalidation" or "after",
            RulePhase.Default => ruleType == RuleTypes.Default,
            RulePhase.Automation => ruleType == RuleTypes.Automation,
            _ => false
        };
    }
}
