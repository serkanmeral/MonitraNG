using MongoDB.Bson.Serialization.Attributes;

namespace MngKeeper.Domain.Entities;

/// <summary>
/// Domain-scoped LDAP/AD bind settings (Keycloak user federation / discovery reuse).
/// Password is stored in plain text for now (ops-managed domains).
/// </summary>
[BsonIgnoreExtraElements]
public class DirectoryLdapSettings
{
    [BsonElement("enabled")]
    public bool Enabled { get; set; }

    /// <summary>LDAP/AD host or FQDN (e.g. dc01.corp.local).</summary>
    [BsonElement("host")]
    public string Host { get; set; } = string.Empty;

    [BsonElement("port")]
    public int Port { get; set; } = 389;

    [BsonElement("useSsl")]
    public bool UseSsl { get; set; }

    /// <summary>Search base DN (e.g. DC=corp,DC=local).</summary>
    [BsonElement("baseDn")]
    public string BaseDn { get; set; } = string.Empty;

    /// <summary>Bind DN or UPN (e.g. CN=svc-ldap,OU=Service,DC=corp,DC=local).</summary>
    [BsonElement("bindUsername")]
    public string BindUsername { get; set; } = string.Empty;

    /// <summary>Bind password (plain text storage for now).</summary>
    [BsonElement("bindPassword")]
    public string BindPassword { get; set; } = string.Empty;
}
