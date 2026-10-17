using MongoDB.Bson.Serialization.Attributes;

namespace MngKeeper.Domain.Entities;

/// <summary>
/// Domain-scoped SMTP settings used by MngNotifier.
/// Password is stored in plain text (same as directory LDAP). Public domain APIs clear it
/// and set <see cref="PasswordConfigured"/>; an empty password on update keeps the stored value.
/// </summary>
[BsonIgnoreExtraElements]
public class DomainMailSettings
{
    [BsonElement("enabled")]
    public bool Enabled { get; set; }

    [BsonElement("host")]
    public string Host { get; set; } = string.Empty;

    [BsonElement("port")]
    public int Port { get; set; } = 587;

    [BsonElement("username")]
    public string Username { get; set; } = string.Empty;

    [BsonElement("password")]
    public string Password { get; set; } = string.Empty;

    [BsonElement("enableSsl")]
    public bool EnableSsl { get; set; } = true;

    /// <summary>Auto, StartTls, SslOnConnect, None.</summary>
    [BsonElement("secureSocketMode")]
    public string SecureSocketMode { get; set; } = "Auto";

    [BsonElement("fromEmail")]
    public string FromEmail { get; set; } = string.Empty;

    [BsonElement("fromName")]
    [BsonIgnoreIfNull]
    public string? FromName { get; set; }

    /// <summary>Set only on API responses. Not persisted.</summary>
    [BsonIgnore]
    public bool PasswordConfigured { get; set; }
}
