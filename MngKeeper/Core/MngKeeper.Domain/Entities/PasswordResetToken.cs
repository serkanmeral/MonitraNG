using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MngKeeper.Domain.Entities
{
    public class PasswordResetToken
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("token")]
        [BsonRequired]
        public string Token { get; set; } = string.Empty;

        [BsonElement("userId")]
        [BsonRequired]
        public string UserId { get; set; } = string.Empty;

        [BsonElement("domainId")]
        [BsonRequired]
        public string DomainId { get; set; } = string.Empty;

        [BsonElement("expiresAt")]
        [BsonRequired]
        public DateTime ExpiresAt { get; set; }

        [BsonElement("isUsed")]
        public bool IsUsed { get; set; } = false;

        [BsonElement("usedAt")]
        public DateTime? UsedAt { get; set; }

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("ipAddress")]
        public string? IpAddress { get; set; }
    }
}

