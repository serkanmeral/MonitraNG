namespace MngAdmin.Application.Backup.DTOs;

/// <summary>
/// Response DTO for backup list operations
/// </summary>
public class BackupListResponseDto
{
    /// <summary>
    /// List of backups
    /// </summary>
    public List<BackupResponseDto> Backups { get; set; } = new();

    /// <summary>
    /// Total count of backups
    /// </summary>
    public int TotalCount { get; set; }
}
