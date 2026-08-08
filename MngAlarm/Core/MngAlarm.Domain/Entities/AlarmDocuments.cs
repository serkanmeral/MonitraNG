using MongoDB.Bson.Serialization.Attributes;
using MngAlarm.Domain.Enums;

namespace MngAlarm.Domain.Entities;

[BsonIgnoreExtraElements]
public class AlarmRuleDocument
{
    [BsonId]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [BsonElement("domainId")]
    public string DomainId { get; set; } = string.Empty;

    [BsonElement("domainName")]
    public string DomainName { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("enabled")]
    public bool Enabled { get; set; } = true;

    [BsonElement("type")]
    public string Type { get; set; } = "threshold";

    [BsonElement("severity")]
    public int Severity { get; set; } = 5;

    [BsonElement("matchKey")]
    public string MatchKey { get; set; } = string.Empty;

    [BsonElement("operator")]
    public string Operator { get; set; } = "gt";

    [BsonElement("threshold")]
    public double Threshold { get; set; }

    [BsonElement("dedupKeyTemplate")]
    public string DedupKeyTemplate { get; set; } = "{ruleId}:{key}";

    [BsonElement("cooldownMinutes")]
    public int CooldownMinutes { get; set; } = 5;

    /// <summary>Correlation: dimension keys to group events (e.g. userId, srcIp).</summary>
    [BsonElement("groupByFields")]
    public List<string> GroupByFields { get; set; } = [];

    /// <summary>Correlation: sliding window size in minutes.</summary>
    [BsonElement("windowMinutes")]
    public int WindowMinutes { get; set; } = 5;

    /// <summary>Scheduled validation: raise if no matching observation within this many minutes.</summary>
    [BsonElement("stalenessMinutes")]
    public int StalenessMinutes { get; set; }

    /// <summary>Sequence: ordered steps (U2 fail→success).</summary>
    [BsonElement("sequenceSteps")]
    public List<AlarmSequenceStep> SequenceSteps { get; set; } = [];

    /// <summary>SIEM paket / MITRE / ISO metadata (B3).</summary>
    [BsonElement("metadata")]
    public AlarmRuleMetadata? Metadata { get; set; }

    /// <summary>Canonical Scenario Definition v2. Null means this is an unmigrated legacy rule.</summary>
    [BsonElement("definition")]
    [BsonIgnoreIfNull]
    public ScenarioDefinition? Definition { get; set; }

    [BsonElement("scenarioId")]
    [BsonIgnoreIfNull]
    public string? ScenarioId { get; set; }

    [BsonElement("scenarioVersion")]
    [BsonIgnoreIfDefault]
    public int ScenarioVersion { get; set; }

    /// <summary>Runtime health for scenario-backed rules (eval / side-effect failures).</summary>
    [BsonElement("runtimeHealth")]
    [BsonIgnoreIfNull]
    public ScenarioRuntimeHealth? RuntimeHealth { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[BsonIgnoreExtraElements]
public class AlarmDocument
{
    [BsonId]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [BsonElement("domainId")]
    public string DomainId { get; set; } = string.Empty;

    [BsonElement("domainName")]
    public string DomainName { get; set; } = string.Empty;

    [BsonElement("ruleId")]
    public string RuleId { get; set; } = string.Empty;

    [BsonElement("dedupKey")]
    public string DedupKey { get; set; } = string.Empty;

    [BsonElement("severity")]
    public int Severity { get; set; }

    [BsonElement("status")]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public AlarmStatus Status { get; set; } = AlarmStatus.Active;

    [BsonElement("firstSeenAt")]
    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;

    [BsonElement("lastSeenAt")]
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    [BsonElement("count")]
    public int Count { get; set; } = 1;

    [BsonElement("context")]
    public Dictionary<string, object?> Context { get; set; } = new();

    [BsonElement("correlationId")]
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");

    [BsonElement("lastPublishedAt")]
    public DateTime? LastPublishedAt { get; set; }
}

[BsonIgnoreExtraElements]
public class AlarmNotificationPolicyDocument
{
    [BsonId]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [BsonElement("domainId")]
    public string DomainId { get; set; } = string.Empty;

    [BsonElement("domainName")]
    public string DomainName { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("eventType")]
    public string EventType { get; set; } = string.Empty;

    [BsonElement("ruleId")]
    public string? RuleId { get; set; }

    [BsonElement("minSeverity")]
    public int? MinSeverity { get; set; }

    [BsonElement("maxSeverity")]
    public int? MaxSeverity { get; set; }

    [BsonElement("channels")]
    public List<string> Channels { get; set; } = [];

    [BsonElement("recipientPersonIds")]
    public List<string> RecipientPersonIds { get; set; } = [];

    [BsonElement("emailTemplateKey")]
    public string? EmailTemplateKey { get; set; }

    [BsonElement("emailSubject")]
    public string? EmailSubject { get; set; }

    [BsonElement("settings")]
    public AlarmNotificationPolicySettings? Settings { get; set; }

    [BsonElement("cooldownMinutes")]
    public int? CooldownMinutes { get; set; }

    [BsonElement("excludeAcknowledgedBy")]
    public bool ExcludeAcknowledgedBy { get; set; }

    [BsonElement("priority")]
    public int? Priority { get; set; }

    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[BsonIgnoreExtraElements]
public class AlarmNotificationPolicySettings
{
    [BsonElement("pushToast")]
    public bool? PushToast { get; set; }

    [BsonElement("toastSeverity")]
    public string? ToastSeverity { get; set; }
}
