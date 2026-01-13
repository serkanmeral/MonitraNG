namespace MngAdmin.Application.Backup.Services;

/// <summary>
/// MinIO backup storage service interface
/// </summary>
public interface IMinioBackupService
{
    /// <summary>
    /// Upload backup file to MinIO
    /// </summary>
    Task<bool> UploadBackupAsync(string bucketName, string objectPath, Stream backupStream, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// List backup files for a database
    /// </summary>
    Task<List<BackupFileInfo>> ListBackupsAsync(string bucketName, string backupPath, string databaseName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete backup file from MinIO
    /// </summary>
    Task<bool> DeleteBackupAsync(string bucketName, string objectPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get backup file info
    /// </summary>
    Task<BackupFileInfo?> GetBackupInfoAsync(string bucketName, string objectPath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Backup file information
/// </summary>
public class BackupFileInfo
{
    public string ObjectPath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime LastModified { get; set; }
    public string DatabaseName { get; set; } = string.Empty;
    public DateTime BackupDate { get; set; }
}
