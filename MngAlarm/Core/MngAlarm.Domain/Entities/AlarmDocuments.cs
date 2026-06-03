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
