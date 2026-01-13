namespace MngAdmin.Application.Backup.Services;

/// <summary>
/// Database backup service interface for creating backups
/// </summary>
public interface IDatabaseBackupService
{
    /// <summary>
    /// Create MongoDB backup
    /// </summary>
    Task<Stream> CreateMongoBackupAsync(string databaseName, string connectionString, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create PostgreSQL backup
    /// </summary>
    Task<Stream> CreatePostgresBackupAsync(string databaseName, string connectionString, CancellationToken cancellationToken = default);
}
