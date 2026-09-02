namespace MngLogs.Agent.Dlp;

/// <summary>First-matching DLP engine (POLICY.md §3.2). Identity groups are Dilim 1 unresolved.</summary>
public static class DlpEngine
{
    public static DlpEvaluateResponse Evaluate(
        DlpCompiledPolicy policy,
        DlpEvaluateRequest request,
        IReadOnlyList<DlpClassificationHit> attachmentClasses,
        string? localEnforcementMode = null)
    {
        var correlationId = Guid.NewGuid().ToString("D");
        var mode = NormalizeMode(
            string.IsNullOrWhiteSpace(localEnforcementMode) ? policy.EnforcementMode : localEnforcementMode);
        var action = string.IsNullOrWhiteSpace(request.Action) ? "email.send" : request.Action.Trim();
        var emailScope = ResolveEmailScope(request.Recipients, policy.Dictionaries.InternalEmailDomains);
        var identity = new DlpIdentityHit
        {
            WindowsUser = request.WindowsUser ?? string.Empty,
            Source = "unresolved"
        };

        var hit = SelectPrimary(attachmentClasses);
        if (hit is null || string.IsNullOrWhiteSpace(hit.Id))
        {
            var unclassifiedEffect = NormalizeEffect(policy.Unclassified.Effect);
            return Finish(
                correlationId,
                policy,
                mode,
                unclassifiedEffect,
                allowByEffect: policy.Unclassified.Allow,
                hit: new DlpClassificationHit { Source = "none", Sensitivity = 0 },
                emailScope,
                identity,
                matched: null);
        }

        var rule = policy.Rules
            .Where(r => r.Enabled)
            .OrderBy(r => r.Priority)
            .FirstOrDefault(r => Matches(r, hit, action, emailScope, identity.GroupIds));

        if (rule is null)
        {
            return Finish(
                correlationId,
                policy,
                mode,
                "audit",
                allowByEffect: true,
                hit,
                emailScope,
                identity,
                matched: null);
        }

        return Finish(
            correlationId,
            policy,
            mode,
            NormalizeEffect(rule.Effect),
            allowByEffect: !string.Equals(rule.Effect, "block", StringComparison.OrdinalIgnoreCase),
            hit,
            emailScope,
            identity,
            rule);
    }

    public static string ResolveEmailScope(IEnumerable<string>? recipients, IEnumerable<string>? internalDomains)
    {
        var internals = new HashSet<string>(
            (internalDomains ?? []).Select(NormalizeDomain).Where(s => s.Length > 0),
            StringComparer.OrdinalIgnoreCase);
        var any = false;
        var anyExternal = false;
        foreach (var raw in recipients ?? [])
        {
            var domain = DomainOf(raw);
            if (domain.Length == 0)
                continue;
            any = true;
            if (internals.Count == 0 || !internals.Contains(domain))
                anyExternal = true;
        }

        if (!any)
            return "any";
        return anyExternal ? "external" : "internal";
    }

    private static bool Matches(
        DlpRule rule,
        DlpClassificationHit hit,
        string action,
        string emailScope,
        IReadOnlyList<string> groupIds)
    {
        if (!rule.Actions.Any(a => string.Equals(a, action, StringComparison.OrdinalIgnoreCase)))
            return false;

        var classOk = rule.ClassificationIds.Any(id =>
            id == "*" || string.Equals(id, hit.Id, StringComparison.OrdinalIgnoreCase));
        if (!classOk)
            return false;

        var ruleScope = string.IsNullOrWhiteSpace(rule.Destination?.EmailScope)
            ? "any"
            : rule.Destination.EmailScope.Trim().ToLowerInvariant();
        if (ruleScope is not "any" && !string.Equals(ruleScope, emailScope, StringComparison.OrdinalIgnoreCase))
            return false;

        if (rule.ExceptGroupIds.Count > 0 &&
            groupIds.Any(g => rule.ExceptGroupIds.Contains(g, StringComparer.OrdinalIgnoreCase)))
            return false;

        return true;
    }

    private static DlpClassificationHit? SelectPrimary(IReadOnlyList<DlpClassificationHit> hits)
    {
        return hits
            .Where(h => !string.IsNullOrWhiteSpace(h.Id))
            .OrderByDescending(h => h.Sensitivity)
            .FirstOrDefault();
    }

    private static DlpEvaluateResponse Finish(
        string correlationId,
        DlpCompiledPolicy policy,
        string mode,
        string effect,
        bool allowByEffect,
        DlpClassificationHit hit,
        string emailScope,
        DlpIdentityHit identity,
        DlpRule? matched)
    {
        var auditOnly = string.Equals(mode, "auditOnly", StringComparison.OrdinalIgnoreCase);
        var wouldBlock = string.Equals(effect, "block", StringComparison.OrdinalIgnoreCase);
        var allowSend = auditOnly || allowByEffect;
        var decision = allowSend ? "allow" : (string.Equals(effect, "warn", StringComparison.OrdinalIgnoreCase) ? "warn" : "block");

        return new DlpEvaluateResponse
        {
            CorrelationId = correlationId,
            PolicyVersion = policy.Version,
            EnforcementMode = mode,
            Decision = decision,
            Effect = effect,
            AllowSend = allowSend,
            WouldBlock = wouldBlock,
            Classification = hit,
            EmailScope = emailScope,
            Identity = identity,
            MatchedRuleId = matched?.Id,
            MatchedRuleName = matched?.Name,
            Prompt = new DlpPrompt { Kind = "none" },
            Message = allowSend ? null : matched?.Name
        };
    }

    private static string NormalizeMode(string? mode) =>
        string.Equals(mode, "enforce", StringComparison.OrdinalIgnoreCase) ? "enforce" : "auditOnly";

    private static string NormalizeEffect(string? effect)
    {
        if (string.Equals(effect, "block", StringComparison.OrdinalIgnoreCase)) return "block";
        if (string.Equals(effect, "warn", StringComparison.OrdinalIgnoreCase)) return "warn";
        return "audit";
    }

    private static string DomainOf(string? recipient)
    {
        var value = (recipient ?? string.Empty).Trim().Trim('<', '>');
        var at = value.LastIndexOf('@');
        if (at < 0 || at == value.Length - 1)
            return string.Empty;
        return NormalizeDomain(value[(at + 1)..]);
    }

    private static string NormalizeDomain(string? domain) =>
        (domain ?? string.Empty).Trim().TrimEnd('.').ToLowerInvariant();
}
