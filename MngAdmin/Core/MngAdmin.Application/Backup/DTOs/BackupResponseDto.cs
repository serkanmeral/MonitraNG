namespace MngAdmin.Application.Backup.DTOs;

/// <summary>
/// Response DTO for backup operations
/// </summary>
public class BackupResponseDto
{
    /// <summary>
    /// Backup status ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Backup type: "system" or "domain"
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Database name
    /// </summary>
    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>
    /// Domain name (null for system backups)
    /// </summary>
    public string? DomainName { get; set; }

    /// <summary>
    /// Backup status: "in_progress", "completed", "failed"
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Backup start time
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Backup completion time
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Backup duration in milliseconds
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// Backup file size in bytes
    /// </summary>
    public long? SizeBytes { get; set; }

    /// <summary>
    /// Error message if backup failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Backup file path in MinIO
    /// </summary>
    public string BackupPath { get; set; } = string.Empty;
}
