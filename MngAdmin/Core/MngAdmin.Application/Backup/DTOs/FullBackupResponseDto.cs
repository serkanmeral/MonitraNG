namespace MngAdmin.Application.Backup.DTOs;

/// <summary>
/// Full backup response DTO
/// Contains results of all backup operations (system and domain backups)
/// </summary>
public class FullBackupResponseDto
{
    /// <summary>
    /// Full backup operation ID (unique identifier for this full backup run)
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Overall status of the full backup operation
    /// </summary>
    public string Status { get; set; } = "in_progress";

    /// <summary>
    /// Start time of the full backup operation
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// Completion time of the full backup operation
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Total duration in milliseconds
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// System backup results (MongoDB and PostgreSQL)
    /// </summary>
    public List<BackupResponseDto> SystemBackups { get; set; } = new();

    /// <summary>
    /// Domain backup results
    /// </summary>
    public List<BackupResponseDto> DomainBackups { get; set; } = new();

    /// <summary>
    /// Total number of backups created
    /// </summary>
    public int TotalBackups { get; set; }

    /// <summary>
    /// Number of successful backups
    /// </summary>
    public int SuccessfulBackups { get; set; }

    /// <summary>
    /// Number of failed backups
    /// </summary>
    public int FailedBackups { get; set; }

    /// <summary>
    /// Error message if the full backup operation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// List of domain names that were backed up
    /// </summary>
    public List<string> DomainsBackedUp { get; set; } = new();
}
