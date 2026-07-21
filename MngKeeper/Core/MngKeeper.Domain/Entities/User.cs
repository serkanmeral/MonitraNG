using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MngKeeper.Domain.Enums;

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
        public string? Email { get; set; }

        [BsonElement("firstName")]
        public string FirstName { get; set; } = string.Empty;

        [BsonElement("lastName")]
        public string LastName { get; set; } = string.Empty;

        [BsonElement("title")]
        public string? Title { get; set; }

        [BsonElement("department")]
        public string? Department { get; set; }

        [BsonElement("gender")]
        [BsonRepresentation(BsonType.Int32)]
        public Gender Gender { get; set; } = Gender.NotSpecified;

        [BsonElement("phoneNumber")]
        public string? PhoneNumber { get; set; }

        /// <summary>Telegram @username without leading @ (display / search). Not enough to send.</summary>
        [BsonElement("telegramUsername")]
        public string? TelegramUsername { get; set; }

        /// <summary>Telegram chat_id for Bot API sendMessage (DM). Required for personal notify.</summary>
        [BsonElement("telegramChatId")]
        public string? TelegramChatId { get; set; }

        /// <summary>When telegramChatId was last bound.</summary>
        [BsonElement("telegramLinkedAt")]
        public DateTime? TelegramLinkedAt { get; set; }

        [BsonElement("photoUrl")]
        public string? PhotoUrl { get; set; }

        [BsonElement("photoSource")]
        [BsonRepresentation(BsonType.Int32)]
        public UserPhotoSource PhotoSource { get; set; } = UserPhotoSource.None;

        /// <summary>Directory kaynaklı fotoğrafın SHA-256 özeti (değişim tespiti).</summary>
        [BsonElement("directoryPhotoHash")]
        public string? DirectoryPhotoHash { get; set; }

        [BsonElement("isActive")]
        public bool IsActive { get; set; } = true;

        /// <summary>MonitraNG uygulama kapsamında mı (picker/liste + login). Sync yazmaz.</summary>
        [BsonElement("includeInApplication")]
        public bool IncludeInApplication { get; set; } = true;

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

        [BsonElement("provisioningSource")]
        [BsonRepresentation(BsonType.Int32)]
        public UserProvisioningSource ProvisioningSource { get; set; } = UserProvisioningSource.Local;

        [BsonElement("directorySyncedAt")]
        public DateTime? DirectorySyncedAt { get; set; }
    }
}
