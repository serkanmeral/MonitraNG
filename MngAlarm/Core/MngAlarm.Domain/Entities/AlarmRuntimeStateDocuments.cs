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
}
