namespace MngAdmin.Application.Backup.DTOs;

/// <summary>
/// Request DTO for backup operations
/// </summary>
public class BackupRequestDto
{
    /// <summary>
    /// Database type: "mongodb" or "postgresql" (optional, defaults to "mongodb" for domain backups)
    /// </summary>
    public string? DatabaseType { get; set; }

    /// <summary>
    /// Specific database name to backup (optional, if not provided, all databases of the type will be backed up)
    /// </summary>
    public string? DatabaseName { get; set; }
}
