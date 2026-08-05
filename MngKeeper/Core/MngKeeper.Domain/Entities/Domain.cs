using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MngKeeper.Domain.Entities
{
    [BsonIgnoreExtraElements]
    public class Domain
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("name")]
        public string Name { get; set; } = string.Empty;

        [BsonElement("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [BsonElement("discoveryRootLabel")]
        [BsonIgnoreIfNull]
        public string? DiscoveryRootLabel { get; set; }

        [BsonElement("databaseName")]
        public string DatabaseName { get; set; } = string.Empty;

        [BsonElement("realmName")]
        public string RealmName { get; set; } = string.Empty;

        [BsonElement("storageBucket")]
        public string StorageBucket { get; set; } = string.Empty;

        [BsonElement("storageQuota")]
        public long StorageQuota { get; set; } = 10737418240;  // 10GB default

        [BsonElement("storageUsed")]
        public long StorageUsed { get; set; } = 0;

        [BsonElement("status")]
        public DomainStatus Status { get; set; } = DomainStatus.Pending;

        [BsonElement("settings")]
        public DomainSettings Settings { get; set; } = new();

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("expiresAt")]
        public DateTime? ExpiresAt { get; set; }

        [BsonElement("createdBy")]
        public string CreatedBy { get; set; } = string.Empty;

        [BsonElement("updatedAt")]
        public DateTime? UpdatedAt { get; set; }

        [BsonElement("updatedBy")]
        public string? UpdatedBy { get; set; }

        [BsonElement("relatedPersonPhone")]
        public string? RelatedPersonPhone { get; set; }

        [BsonElement("logo")]
        public string? Logo { get; set; }

        [BsonElement("logoUrl")]
        public string? LogoUrl { get; set; }

        [BsonElement("licenseInfo")]
        public LicenseInfo LicenseInfo { get; set; } = new();
    }

    public enum DomainStatus
    {
        Pending,
        Active,
        Suspended,
        Expired,
        Deleted,
        Failed
    }

    [BsonIgnoreExtraElements]
    public class DomainSettings
    {
        [BsonElement("maxUsers")]
        public int MaxUsers { get; set; } = 100;

        [BsonElement("maxAssets")]
        public int MaxAssets { get; set; } = 1000;

        [BsonElement("enableMqtt")]
        public bool EnableMqtt { get; set; } = true;

        [BsonElement("mqttSettings")]
        public MqttSettings MqttSettings { get; set; } = new();

        [BsonElement("customSettings")]
        public Dictionary<string, object> CustomSettings { get; set; } = new();

        [BsonElement("directoryPrivileges")]
        public DirectoryPrivilegeSettings DirectoryPrivileges { get; set; } = new();

        /// <summary>
        /// LDAP/AD bind for Keycloak federation and services that need directory access (e.g. discovery).
        /// Null when not configured; omit from partial update payloads to leave unchanged.
        /// </summary>
        [BsonElement("directoryLdap")]
        [BsonIgnoreIfNull]
        public DirectoryLdapSettings? DirectoryLdap { get; set; }

        /// <summary>
        /// Manuel Mongo güncellemelerinde yanlışlıkla settings altına düz yazılmış alanlar.
        /// </summary>
        [BsonElement("adminGroupNames")]
        [BsonIgnoreIfNull]
        public List<string>? FlatAdminGroupNames { get; set; }

        [BsonElement("managerGroupNames")]
        [BsonIgnoreIfNull]
        public List<string>? FlatManagerGroupNames { get; set; }

        public DirectoryPrivilegeSettings ResolveDirectoryPrivileges()
        {
            var nested = DirectoryPrivileges ?? new DirectoryPrivilegeSettings();
            var admin = FlatAdminGroupNames is { Count: > 0 }
                ? FlatAdminGroupNames
                : nested.AdminGroupNames;
            var manager = FlatManagerGroupNames is { Count: > 0 }
                ? FlatManagerGroupNames
                : nested.ManagerGroupNames;

            if (ReferenceEquals(admin, nested.AdminGroupNames) &&
                ReferenceEquals(manager, nested.ManagerGroupNames))
            {
                return nested;
            }

            return new DirectoryPrivilegeSettings
            {
                AdminGroupNames = admin ?? new List<string>(),
                ManagerGroupNames = manager ?? new List<string>()
            };
        }
    }

    public class MqttSettings
    {
        [BsonElement("brokerHost")]
        public string BrokerHost { get; set; } = "mosquitto";

        [BsonElement("brokerPort")]
        public int BrokerPort { get; set; } = 1883;

        [BsonElement("username")]
        public string Username { get; set; } = string.Empty;

        [BsonElement("password")]
        public string Password { get; set; } = string.Empty;

        [BsonElement("topicPrefix")]
        public string TopicPrefix { get; set; } = "MNG";
    }
}
