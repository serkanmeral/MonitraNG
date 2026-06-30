using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MngKeeper.Domain.Enums;

namespace MngKeeper.Domain.Entities
{
    public class Group
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("domainId")]
        public string DomainId { get; set; } = string.Empty;

        [BsonElement("keycloakGroupId")]
        public string KeycloakGroupId { get; set; } = string.Empty;

        [BsonElement("name")]
        public string Name { get; set; } = string.Empty;

        [BsonElement("description")]
        public string Description { get; set; } = string.Empty;

        [BsonElement("permissions")]
        public List<string> Permissions { get; set; } = new();

        [BsonElement("isActive")]
        public bool IsActive { get; set; } = true;

        /// <summary>MonitraNG uygulama kapsamında mı (picker/liste). Sync yazmaz.</summary>
        [BsonElement("includeInApplication")]
        public bool IncludeInApplication { get; set; } = true;

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("createdBy")]
        public string CreatedBy { get; set; } = string.Empty;

        [BsonElement("updatedAt")]
        public DateTime? UpdatedAt { get; set; }

        [BsonElement("updatedBy")]
        public string? UpdatedBy { get; set; }

        [BsonElement("provisioningSource")]
        public UserProvisioningSource ProvisioningSource { get; set; } = UserProvisioningSource.Local;

        [BsonElement("directorySyncedAt")]
        public DateTime? DirectorySyncedAt { get; set; }
    }
}
