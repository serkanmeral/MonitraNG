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
