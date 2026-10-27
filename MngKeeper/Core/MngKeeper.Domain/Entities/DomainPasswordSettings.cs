using MongoDB.Bson.Serialization.Attributes;

namespace MngKeeper.Domain.Entities;

/// <summary>
/// Where a domain stores passwords. <see cref="Keycloak"/> is the default.
/// <see cref="KeycloakAndAd"/> also writes directory users' passwords to Active Directory.
/// </summary>
public static class DomainPasswordModes
{
    public const string Keycloak = "keycloak";
    public const string KeycloakAndAd = "keycloak-and-ad";
}

public class DomainPasswordSettings
{
    [BsonElement("mode")]
    public string Mode { get; set; } = DomainPasswordModes.Keycloak;

    /// <summary>Admin reset only. Self-service searches the directory base DN and does not use this container.</summary>
    [BsonElement("personnelDn")]
    public string PersonnelDn { get; set; } = string.Empty;

    /// <summary>Admin reset bind. Self-service binds as the user and does not use this account.</summary>
    [BsonElement("resetBindUsername")]
    public string ResetBindUsername { get; set; } = string.Empty;

    [BsonElement("resetBindPassword")]
    public string ResetBindPassword { get; set; } = string.Empty;

    /// <summary>SHA-1 thumbprint of the LDAPS server certificate. The writer pins this instead of the public chain.</summary>
    [BsonElement("serverCertificateThumbprint")]
    public string ServerCertificateThumbprint { get; set; } = string.Empty;

    [BsonIgnore]
    public bool ResetBindPasswordConfigured { get; set; }
}
