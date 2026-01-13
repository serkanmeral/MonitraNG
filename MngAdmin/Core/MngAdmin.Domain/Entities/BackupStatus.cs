using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MngAdmin.Domain.Entities;

/// <summary>
/// Backup status entity stored in mngkeeper database
/// </summary>
public class BackupStatus
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Backup type: "system" or "domain"
    /// </summary>
    [BsonElement("type")]
    public string Type { get; set; } = string.Empty; // "system" | "domain"

    /// <summary>
    /// Database name (e.g., "mngkeeper", "mng_meral", "keycloak")
    /// </summary>
    [BsonElement("databaseName")]
    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>
    /// Domain name (null for system backups, e.g., "meral" for domain backups)
    /// </summary>
    [BsonElement("domainName")]
    public string? DomainName { get; set; }

    /// <summary>
    /// Backup file path in MinIO (e.g., "system/backup/mongodb/mngkeeper_20250115_120000.zip")
    /// </summary>
    [BsonElement("backupPath")]
    public string BackupPath { get; set; } = string.Empty;

    /// <summary>
    /// Backup status: "in_progress", "completed", "failed"
    /// </summary>
    [BsonElement("status")]
    public string Status { get; set; } = "in_progress"; // "in_progress" | "completed" | "failed"

    /// <summary>
    /// Backup start time
    /// </summary>
    [BsonElement("startedAt")]
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Backup completion time (null if in progress or failed)
    /// </summary>
    [BsonElement("completedAt")]
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Backup duration in milliseconds
    /// </summary>
    [BsonElement("durationMs")]
    public long? DurationMs { get; set; }

    /// <summary>
    /// Backup file size in bytes
    /// </summary>
    [BsonElement("sizeBytes")]
    public long? SizeBytes { get; set; }

    /// <summary>
    /// Error message if backup failed
    /// </summary>
    [BsonElement("errorMessage")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// User ID who initiated the backup
    /// </summary>
    [BsonElement("createdBy")]
    public string CreatedBy { get; set; } = string.Empty;
}
