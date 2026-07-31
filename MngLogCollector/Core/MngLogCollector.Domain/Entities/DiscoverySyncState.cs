using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MngLogCollector.Domain.Entities;

[BsonIgnoreExtraElements]
public sealed class DiscoverySyncState
{
    [BsonId]
    public string Id { get; set; } = "ad";

    [BsonElement("domainId")]
    public string DomainId { get; set; } = string.Empty;

    [BsonElement("lastSyncAt")]
    [BsonIgnoreIfNull]
    public DateTime? LastSyncAt { get; set; }

    [BsonElement("lastSyncStatus")]
    public string LastSyncStatus { get; set; } = "never";

    [BsonElement("lastSyncError")]
    [BsonIgnoreIfNull]
    public string? LastSyncError { get; set; }

    [BsonElement("lastSyncRunId")]
    [BsonIgnoreIfNull]
    public string? LastSyncRunId { get; set; }

    [BsonElement("pulled")]
    public int Pulled { get; set; }

    [BsonElement("upserted")]
    public int Upserted { get; set; }

    [BsonElement("durationMs")]
    public long DurationMs { get; set; }
}
