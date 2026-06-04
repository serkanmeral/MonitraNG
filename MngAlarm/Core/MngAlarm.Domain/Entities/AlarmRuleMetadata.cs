using MongoDB.Bson.Serialization.Attributes;

namespace MngAlarm.Domain.Entities;

/// <summary>SIEM hazır kural paketi — MITRE ATT&amp;CK ve uyum etiketleri (B3).</summary>
[BsonIgnoreExtraElements]
public sealed class AlarmRuleMetadata
{
    [BsonElement("packageId")]
    public string PackageId { get; set; } = string.Empty;

    [BsonElement("packageVersion")]
    public string PackageVersion { get; set; } = string.Empty;

    [BsonElement("scenarioId")]
    public string ScenarioId { get; set; } = string.Empty;

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    [BsonElement("threatTacticId")]
    public string ThreatTacticId { get; set; } = string.Empty;

    [BsonElement("threatTacticName")]
    public string ThreatTacticName { get; set; } = string.Empty;

    [BsonElement("threatTechniqueId")]
    public string ThreatTechniqueId { get; set; } = string.Empty;

    [BsonElement("threatTechniqueName")]
    public string ThreatTechniqueName { get; set; } = string.Empty;

    [BsonElement("complianceTags")]
    public List<string> ComplianceTags { get; set; } = [];
}
