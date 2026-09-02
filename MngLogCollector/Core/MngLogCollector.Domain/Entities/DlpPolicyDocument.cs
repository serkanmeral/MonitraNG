using MongoDB.Bson.Serialization.Attributes;

namespace MngLogCollector.Domain.Entities;

/// <summary>Draft or published DLP policy snapshot. Id is <c>draft</c> or <c>published</c>.</summary>
public sealed class DlpPolicyDocument
{
    public const string DraftId = "draft";
    public const string PublishedId = "published";

    [BsonId]
    public string Id { get; set; } = DraftId;

    public int SchemaVersion { get; set; } = 1;
    public string PolicyId { get; set; } = "odak-default";
    public string EnforcementMode { get; set; } = "auditOnly";
    public DlpUnclassifiedState Unclassified { get; set; } = new();
    public List<DlpClassificationState> Classifications { get; set; } = [];
    public DlpDictionariesState Dictionaries { get; set; } = new();
    public List<DlpRuleState> Rules { get; set; } = [];
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class DlpUnclassifiedState
{
    public bool Allow { get; set; } = true;
    public string Effect { get; set; } = "audit";
}

public sealed class DlpClassificationState
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Sensitivity { get; set; }
    public bool PersistToFile { get; set; } = true;
}

public sealed class DlpDictionariesState
{
    public List<string> InternalEmailDomains { get; set; } = [];
    public List<string> SanctionedProcesses { get; set; } = [];
    public List<string> UnsanctionedProcesses { get; set; } = [];
}

public sealed class DlpRuleState
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; }
    public List<string> ClassificationIds { get; set; } = [];
    public List<string> Actions { get; set; } = [];
    public string EmailScope { get; set; } = "any";
    public List<string> ExceptGroupIds { get; set; } = [];
    public string Effect { get; set; } = "audit";
}

/// <summary>Singleton meta for published DLP version (ETag).</summary>
public sealed class DlpCatalogMetaDocument
{
    public const string SingletonId = "meta";

    [BsonId]
    public string Id { get; set; } = SingletonId;

    public string Version { get; set; } = "0";

    public DateTime PublishedUtc { get; set; } = DateTime.UnixEpoch;
}
