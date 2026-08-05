using MongoDB.Bson.Serialization.Attributes;

namespace MngAlarm.Domain.Entities;

[BsonIgnoreExtraElements]
public class CorrelationWindowDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("domainName")]
    public string DomainName { get; set; } = string.Empty;

    [BsonElement("ruleId")]
    public string RuleId { get; set; } = string.Empty;

    [BsonElement("events")]
    public List<DateTime> Events { get; set; } = [];
}

[BsonIgnoreExtraElements]
public class ObservationActivityDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("domainName")]
    public string DomainName { get; set; } = string.Empty;

    [BsonElement("ruleId")]
    public string RuleId { get; set; } = string.Empty;

    [BsonElement("lastSeenAt")]
    public DateTime LastSeenAt { get; set; }
}

[BsonIgnoreExtraElements]
public class SequenceStateDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("domainName")]
    public string DomainName { get; set; } = string.Empty;

    [BsonElement("ruleId")]
    public string RuleId { get; set; } = string.Empty;

    [BsonElement("nextStepIndex")]
    public int NextStepIndex { get; set; }

    [BsonElement("currentStepCount")]
    public int CurrentStepCount { get; set; }

    [BsonElement("anchorTime")]
    public DateTime? AnchorTime { get; set; }

    [BsonElement("lastStepTime")]
    public DateTime? LastStepTime { get; set; }

    [BsonElement("conditionSince")]
    public DateTime? ConditionSince { get; set; }

    [BsonElement("nextEvaluationAt")]
    public DateTime? NextEvaluationAt { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class ScenarioDueStateDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("domainId")]
    public string DomainId { get; set; } = string.Empty;

    [BsonElement("domainName")]
    public string DomainName { get; set; } = string.Empty;

    [BsonElement("ruleId")]
    public string RuleId { get; set; } = string.Empty;

    [BsonElement("scenarioVersion")]
    public int ScenarioVersion { get; set; }

    [BsonElement("nodeId")]
    public string NodeId { get; set; } = string.Empty;

    [BsonElement("nodeType")]
    public string NodeType { get; set; } = string.Empty;

    [BsonElement("groupKey")]
    public string GroupKey { get; set; } = "_all";

    [BsonElement("nextEvaluationAt")]
    public DateTime NextEvaluationAt { get; set; }

    [BsonElement("observation")]
    public ScenarioDueObservation Observation { get; set; } = new();

    [BsonElement("claimToken")]
    [BsonIgnoreIfNull]
    public string? ClaimToken { get; set; }

    [BsonElement("claimedUntil")]
    [BsonIgnoreIfNull]
    public DateTime? ClaimedUntil { get; set; }

    [BsonElement("attempts")]
    public int Attempts { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[BsonIgnoreExtraElements]
public sealed class ScenarioDueObservation
{
    [BsonElement("kind")]
    public string Kind { get; set; } = string.Empty;

    [BsonElement("key")]
    public string Key { get; set; } = string.Empty;

    [BsonElement("value")]
    public double? Value { get; set; }

    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; }

    [BsonElement("dimensions")]
    public Dictionary<string, object?> Dimensions { get; set; } = new(StringComparer.Ordinal);
}
