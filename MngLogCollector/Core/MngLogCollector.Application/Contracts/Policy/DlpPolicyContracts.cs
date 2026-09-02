namespace MngLogCollector.Application.Contracts.Policy;

/// <summary>Compiled DLP policy (agent <c>GET /api/v1/policy/dlp</c> body). See docs/odak/dlp/POLICY.md §3.</summary>
public sealed class DlpPolicyResponse
{
    public int SchemaVersion { get; set; } = 1;
    public string PolicyId { get; set; } = "odak-default";
    public string Version { get; set; } = "0";
    public DateTime PublishedUtc { get; set; }
    public string EnforcementMode { get; set; } = "auditOnly";
    public DlpUnclassifiedPolicy Unclassified { get; set; } = new();
    public List<DlpClassificationDto> Classifications { get; set; } = [];
    public DlpDictionariesDto Dictionaries { get; set; } = new();
    public List<DlpRuleDto> Rules { get; set; } = [];
}

public sealed class DlpUnclassifiedPolicy
{
    public bool Allow { get; set; } = true;
    public string Effect { get; set; } = "audit";
}

public sealed class DlpClassificationDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Sensitivity { get; set; }
    public bool PersistToFile { get; set; } = true;
}

public sealed class DlpDictionariesDto
{
    public List<string> InternalEmailDomains { get; set; } = [];
    public List<string> SanctionedProcesses { get; set; } = [];
    public List<string> UnsanctionedProcesses { get; set; } = [];
}

public sealed class DlpRuleDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; }
    public List<string> ClassificationIds { get; set; } = [];
    public List<string> Actions { get; set; } = [];
    public DlpDestinationDto Destination { get; set; } = new();
    public List<string> ExceptGroupIds { get; set; } = [];
    public string Effect { get; set; } = "audit";
}

public sealed class DlpDestinationDto
{
    public string EmailScope { get; set; } = "any";
}

public sealed class DlpPolicyManageResponse
{
    public string Version { get; set; } = "0";
    public DateTime PublishedUtc { get; set; }
    public bool HasUnpublishedChanges { get; set; }
    public DlpPolicyResponse Draft { get; set; } = new();
}

/// <summary>Replace the draft policy (publish required before agents see it).</summary>
public sealed class DlpPolicyUpsertRequest
{
    public string? PolicyId { get; set; }
    public string? EnforcementMode { get; set; }
    public DlpUnclassifiedPolicy? Unclassified { get; set; }
    public List<DlpClassificationDto>? Classifications { get; set; }
    public DlpDictionariesDto? Dictionaries { get; set; }
    public List<DlpRuleDto>? Rules { get; set; }
}
