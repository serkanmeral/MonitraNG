using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MngKeeper.Domain.Entities
{
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("domainId")]
        public string DomainId { get; set; } = string.Empty;

        [BsonElement("keycloakUserId")]
        public string KeycloakUserId { get; set; } = string.Empty;

        [BsonElement("username")]
        public string Username { get; set; } = string.Empty;

        [BsonElement("email")]
        public string Email { get; set; } = string.Empty;

        [BsonElement("firstName")]
        public string FirstName { get; set; } = string.Empty;

        [BsonElement("lastName")]
        public string LastName { get; set; } = string.Empty;

        [BsonElement("isActive")]
        public bool IsActive { get; set; } = true;

        [BsonElement("groups")]
        public List<string> Groups { get; set; } = new();

        [BsonElement("roles")]
        public List<string> Roles { get; set; } = new();

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("lastLoginAt")]
        public DateTime? LastLoginAt { get; set; }

        [BsonElement("createdBy")]
        public string CreatedBy { get; set; } = string.Empty;

        [BsonElement("updatedAt")]
        public DateTime? UpdatedAt { get; set; }

        [BsonElement("updatedBy")]
        public string? UpdatedBy { get; set; }
    }
}
