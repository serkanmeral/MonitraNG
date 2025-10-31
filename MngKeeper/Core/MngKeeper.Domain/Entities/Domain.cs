using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MngKeeper.Domain.Entities
{
    public class Domain
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = string.Empty;

        [BsonElement("name")]
        public string Name { get; set; } = string.Empty;

        [BsonElement("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [BsonElement("databaseName")]
        public string DatabaseName { get; set; } = string.Empty;

        [BsonElement("realmName")]
        public string RealmName { get; set; } = string.Empty;

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
    }

    public enum DomainStatus
    {
        Pending,
        Active,
        Suspended,
        Expired,
        Deleted
    }

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
