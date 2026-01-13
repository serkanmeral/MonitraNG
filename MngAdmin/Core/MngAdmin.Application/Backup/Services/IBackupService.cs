using MngAdmin.Application.Backup.DTOs;

namespace MngAdmin.Application.Backup.Services;

/// <summary>
/// Backup service interface
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Create system backup (MongoDB or PostgreSQL)
    /// </summary>
    Task<BackupResponseDto> CreateSystemBackupAsync(BackupRequestDto request, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create domain backup (MongoDB only)
    /// </summary>
    Task<BackupResponseDto> CreateDomainBackupAsync(string domainName, BackupRequestDto request, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get backup status by ID
    /// </summary>
    Task<BackupResponseDto?> GetBackupStatusAsync(string backupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get system backup list
    /// </summary>
    Task<BackupListResponseDto> GetSystemBackupsAsync(string? databaseName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get domain backup list
    /// </summary>
    Task<BackupListResponseDto> GetDomainBackupsAsync(string domainName, string? databaseName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create full backup (system backups + all domain backups)
    /// </summary>
    Task<FullBackupResponseDto> CreateFullBackupAsync(string userId, CancellationToken cancellationToken = default);
}
