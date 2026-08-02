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

    [BsonElement("lastSeenFromScan")]
    [BsonIgnoreIfNull]
    public DateTime? LastSeenFromScan { get; set; }

    /// <summary>windows | linux | unknown — from network scan port profile.</summary>
    [BsonElement("osFamilyHint")]
    [BsonIgnoreIfNull]
    public string? OsFamilyHint { get; set; }

    [BsonElement("openPorts")]
    [BsonIgnoreIfNull]
    public List<int>? OpenPorts { get; set; }

    /// <summary>workstation | server | printer | network_gear | iot_or_other | unknown</summary>
    [BsonElement("deviceRoleHint")]
    [BsonIgnoreIfNull]
    public string? DeviceRoleHint { get; set; }

    /// <summary>high | medium | low</summary>
    [BsonElement("identityConfidence")]
    [BsonIgnoreIfNull]
    public string? IdentityConfidence { get; set; }

    [BsonElement("identitySummary")]
    [BsonIgnoreIfNull]
    public string? IdentitySummary { get; set; }

    [BsonElement("httpTitle")]
    [BsonIgnoreIfNull]
    public string? HttpTitle { get; set; }

    [BsonElement("tlsCommonName")]
    [BsonIgnoreIfNull]
    public string? TlsCommonName { get; set; }

    [BsonElement("sshBanner")]
    [BsonIgnoreIfNull]
    public string? SshBanner { get; set; }

    /// <summary>Matched prefix CIDR from site table (longest-prefix-match).</summary>
    [BsonElement("subnetCidr")]
    [BsonIgnoreIfNull]
    public string? SubnetCidr { get; set; }

    /// <summary>Human site/subnet label from prefix table; "Unscoped" when IP unmatched.</summary>
    [BsonElement("siteLabel")]
    [BsonIgnoreIfNull]
    public string? SiteLabel { get; set; }

    /// <summary>Optional VLAN name from prefix table mapping (operator-defined, not IP heuristic).</summary>
    [BsonElement("vlanName")]
    [BsonIgnoreIfNull]
    public string? VlanName { get; set; }

    [BsonElement("scanRunId")]
    [BsonIgnoreIfNull]
    public string? ScanRunId { get; set; }

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
