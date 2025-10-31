using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MngKeeper.Domain.Entities
{
    public class AuditLog
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [BsonElement("domainId")]
        public string DomainId { get; set; } = string.Empty;

        [BsonElement("userId")]
        public string UserId { get; set; } = string.Empty;

        [BsonElement("username")]
        public string Username { get; set; } = string.Empty;

        [BsonElement("action")]
        public string Action { get; set; } = string.Empty;

        [BsonElement("resourceType")]
        public string ResourceType { get; set; } = string.Empty;

        [BsonElement("resourceId")]
        public string ResourceId { get; set; } = string.Empty;

        [BsonElement("details")]
        public Dictionary<string, object> Details { get; set; } = new();

        [BsonElement("ipAddress")]
        public string? IpAddress { get; set; }

        [BsonElement("userAgent")]
        public string? UserAgent { get; set; }

        [BsonElement("success")]
        public bool Success { get; set; } = true;

        [BsonElement("errorMessage")]
        public string? ErrorMessage { get; set; }
    }

    public static class AuditActions
    {
        public const string Create = "CREATE";
        public const string Read = "READ";
        public const string Update = "UPDATE";
        public const string Delete = "DELETE";
        public const string Login = "LOGIN";
        public const string Logout = "LOGOUT";
        public const string FailedLogin = "FAILED_LOGIN";
        public const string PasswordChange = "PASSWORD_CHANGE";
        public const string DomainCreate = "DOMAIN_CREATE";
        public const string DomainDelete = "DOMAIN_DELETE";
        public const string UserCreate = "USER_CREATE";
        public const string UserDelete = "USER_DELETE";
        public const string GroupCreate = "GROUP_CREATE";
        public const string GroupDelete = "GROUP_DELETE";
    }

    public static class ResourceTypes
    {
        public const string Domain = "DOMAIN";
        public const string User = "USER";
        public const string Group = "GROUP";
        public const string Authentication = "AUTHENTICATION";
        public const string Authorization = "AUTHORIZATION";
    }
}
