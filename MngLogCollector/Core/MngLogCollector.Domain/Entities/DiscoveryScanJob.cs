using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MngLogCollector.Domain.Entities;

[BsonIgnoreExtraElements]
public sealed class DiscoveryScanJob
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("domainId")]
    public string DomainId { get; set; } = string.Empty;

    [BsonElement("databaseName")]
    public string DatabaseName { get; set; } = string.Empty;

    [BsonElement("cidr")]
    public string Cidr { get; set; } = string.Empty;

    [BsonElement("enrichWithAd")]
    public bool EnrichWithAd { get; set; }

    /// <summary>queued | running | completed | failed | cancelled</summary>
    [BsonElement("status")]
    public string Status { get; set; } = "queued";

    [BsonElement("progressPercent")]
    public int ProgressPercent { get; set; }

    [BsonElement("totalTargets")]
    public int TotalTargets { get; set; }

    [BsonElement("probed")]
    public int Probed { get; set; }

    [BsonElement("foundAlive")]
    public int FoundAlive { get; set; }

    [BsonElement("foundWindows")]
    public int FoundWindows { get; set; }

    [BsonElement("foundLinux")]
    public int FoundLinux { get; set; }

    [BsonElement("foundUnknown")]
    public int FoundUnknown { get; set; }

    [BsonElement("upserted")]
    public int Upserted { get; set; }

    [BsonElement("cancelRequested")]
    public bool CancelRequested { get; set; }

    [BsonElement("error")]
    [BsonIgnoreIfNull]
    public string? Error { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("startedAt")]
    [BsonIgnoreIfNull]
    public DateTime? StartedAt { get; set; }

    [BsonElement("completedAt")]
    [BsonIgnoreIfNull]
    public DateTime? CompletedAt { get; set; }
}
