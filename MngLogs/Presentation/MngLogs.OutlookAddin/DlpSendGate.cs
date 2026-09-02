namespace MngLogs.OutlookAddin;

public sealed class DlpSendDecision
{
    public bool CancelSend { get; set; }
    public bool FailOpen { get; set; }
    public bool ShowAuditHint { get; set; }
    public string? UserMessage { get; set; }
}

/// <summary>Dilim 1 send gate: trust agent allowSend; transport failure is fail-open.</summary>
public static class DlpSendGate
{
    public const string ClientKind = "outlook-addin";
    public const string ClientVersion = "0.1.0";

    public static DlpSendDecision FromEvaluate(DlpEvaluateDto? response, bool transportFailed, string? transportError)
    {
        if (transportFailed || response is null)
        {
            var reason = string.IsNullOrWhiteSpace(transportError)
                ? "DLP agent unreachable."
                : transportError!.Trim();
            return new DlpSendDecision
            {
                CancelSend = false,
                FailOpen = true,
                UserMessage = reason + " Send allowed (Dilim 1 fail-open)."
            };
        }

        if (!response.AllowSend)
        {
            return new DlpSendDecision
            {
                CancelSend = true,
                UserMessage = string.IsNullOrWhiteSpace(response.Message)
                    ? "DLP blocked this send."
                    : response.Message
            };
        }

        if (response.WouldBlock)
        {
            var rule = response.MatchedRuleName ?? response.MatchedRuleId ?? "rule";
            return new DlpSendDecision
            {
                CancelSend = false,
                ShowAuditHint = true,
                UserMessage = "DLP audit: this send would be blocked under enforce (" + rule + "). Dilim 1 auditOnly — send continues."
            };
        }

        return new DlpSendDecision { CancelSend = false };
    }

    public static string WindowsUser()
    {
        var domain = Environment.UserDomainName;
        var user = Environment.UserName;
        if (string.IsNullOrWhiteSpace(domain))
            return user;
        return domain + "\\" + user;
    }
}

public sealed class DlpEvaluateDto
{
    public string? CorrelationId { get; set; }
    public string? PolicyVersion { get; set; }
    public string? EnforcementMode { get; set; }
    public string? Decision { get; set; }
    public string? Effect { get; set; }
    public bool AllowSend { get; set; } = true;
    public bool WouldBlock { get; set; }
    public string? MatchedRuleId { get; set; }
    public string? MatchedRuleName { get; set; }
    public string? Message { get; set; }
}
