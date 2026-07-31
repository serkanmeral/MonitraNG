using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MngLogCollector.Domain.Entities;

[BsonIgnoreExtraElements]
public sealed class DiscoveryHost
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    /// <summary>Tenant domain name (e.g. odak), not ObjectId.</summary>
    [BsonElement("domainId")]
    public string DomainId { get; set; } = string.Empty;

    [BsonElement("objectGuid")]
    [BsonIgnoreIfNull]
    public string? ObjectGuid { get; set; }

    [BsonElement("samAccountName")]
    public string SamAccountName { get; set; } = string.Empty;

    [BsonElement("dnsHostName")]
    [BsonIgnoreIfNull]
    public string? DnsHostName { get; set; }

    [BsonElement("displayName")]
    [BsonIgnoreIfNull]
    public string? DisplayName { get; set; }

    [BsonElement("ipAddresses")]
    public List<string> IpAddresses { get; set; } = [];

    [BsonElement("operatingSystem")]
    [BsonIgnoreIfNull]
    public string? OperatingSystem { get; set; }

    [BsonElement("operatingSystemVersion")]
    [BsonIgnoreIfNull]
    public string? OperatingSystemVersion { get; set; }

    [BsonElement("distinguishedName")]
    [BsonIgnoreIfNull]
    public string? DistinguishedName { get; set; }

    [BsonElement("sources")]
    public List<string> Sources { get; set; } = [];

    [BsonElement("lastSeenFromAd")]
    [BsonIgnoreIfNull]
    public DateTime? LastSeenFromAd { get; set; }

    [BsonElement("adEnabled")]
    [BsonIgnoreIfNull]
    public bool? AdEnabled { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("lastSyncRunId")]
    [BsonIgnoreIfNull]
    public string? LastSyncRunId { get; set; }
}
