using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MngLogCollector.Domain.Entities;

/// <summary>Per-domain IPAM-style prefix table for discovery site grouping.</summary>
[BsonIgnoreExtraElements]
public sealed class DiscoveryPrefixTableDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("domainId")]
    public string DomainId { get; set; } = string.Empty;

    [BsonElement("prefixes")]
    public List<DiscoveryPrefixRow> Prefixes { get; set; } = [];

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[BsonIgnoreExtraElements]
public sealed class DiscoveryPrefixRow
{
    [BsonElement("cidr")]
    public string Cidr { get; set; } = string.Empty;

    [BsonElement("label")]
    public string Label { get; set; } = string.Empty;

    /// <summary>Optional operator-mapped VLAN name (not inferred from IP).</summary>
    [BsonElement("vlanName")]
    [BsonIgnoreIfNull]
    public string? VlanName { get; set; }
}
