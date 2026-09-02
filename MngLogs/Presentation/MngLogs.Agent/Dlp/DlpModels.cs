namespace MngLogs.Agent.Dlp;

public sealed class DlpCompiledPolicy
{
    public int SchemaVersion { get; set; } = 1;
    public string PolicyId { get; set; } = "odak-default";
    public string Version { get; set; } = "0";
    public DateTime PublishedUtc { get; set; }
    public string EnforcementMode { get; set; } = "auditOnly";
    public DlpUnclassified Unclassified { get; set; } = new();
    public List<DlpClassification> Classifications { get; set; } = [];
    public DlpDictionaries Dictionaries { get; set; } = new();
    public List<DlpRule> Rules { get; set; } = [];
}

public sealed class DlpUnclassified
{
    public bool Allow { get; set; } = true;
    public string Effect { get; set; } = "audit";
}

public sealed class DlpClassification
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Sensitivity { get; set; }
    public bool PersistToFile { get; set; } = true;
}

public sealed class DlpDictionaries
{
    public List<string> InternalEmailDomains { get; set; } = [];
    public List<string> SanctionedProcesses { get; set; } = [];
    public List<string> UnsanctionedProcesses { get; set; } = [];
}

public sealed class DlpRule
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; }
    public List<string> ClassificationIds { get; set; } = [];
    public List<string> Actions { get; set; } = [];
    public DlpDestination Destination { get; set; } = new();
    public List<string> ExceptGroupIds { get; set; } = [];
    public string Effect { get; set; } = "audit";
}

public sealed class DlpDestination
{
    public string EmailScope { get; set; } = "any";
}

public sealed class DlpEvaluateRequest
{
    public string Action { get; set; } = "email.send";
    public string WindowsUser { get; set; } = string.Empty;
    public List<string> Recipients { get; set; } = [];
    public List<DlpAttachment> Attachments { get; set; } = [];
    public DlpClientInfo? Client { get; set; }
}

public sealed class DlpAttachment
{
    public string? Path { get; set; }
    public string? ClassificationId { get; set; }
}

public sealed class DlpClientInfo
{
    public string? Kind { get; set; }
    public string? Version { get; set; }
}

public sealed class DlpEvaluateResponse
{
    public string CorrelationId { get; set; } = string.Empty;
    public string PolicyVersion { get; set; } = "0";
    public string EnforcementMode { get; set; } = "auditOnly";
    public string Decision { get; set; } = "allow";
    public string Effect { get; set; } = "audit";
    public bool AllowSend { get; set; } = true;
    public bool WouldBlock { get; set; }
    public DlpClassificationHit? Classification { get; set; }
    public string EmailScope { get; set; } = "any";
    public DlpIdentityHit Identity { get; set; } = new();
    public string? MatchedRuleId { get; set; }
    public string? MatchedRuleName { get; set; }
    public DlpPrompt Prompt { get; set; } = new();
    public string? Message { get; set; }
}

public sealed class DlpClassificationHit
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public int Sensitivity { get; set; }
    public string Source { get; set; } = "none";
}

public sealed class DlpIdentityHit
{
    public string WindowsUser { get; set; } = string.Empty;
    public string? KeeperUserId { get; set; }
    public List<string> GroupIds { get; set; } = [];
    public string Source { get; set; } = "unresolved";
}

public sealed class DlpPrompt
{
    public string Kind { get; set; } = "none";
}

public sealed class DlpPolicyPullResult
{
    public bool Success { get; init; }
    public bool NotModified { get; init; }
    public DlpCompiledPolicy? Policy { get; init; }

    public static DlpPolicyPullResult Failed() => new() { Success = false };
    public static DlpPolicyPullResult Unchanged() => new() { Success = true, NotModified = true };
    public static DlpPolicyPullResult Ok(DlpCompiledPolicy policy) =>
        new() { Success = true, Policy = policy };
}
