using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngAdmin.Application.Backup.Services;
using MngAdmin.Application.Configuration;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using System.Text.RegularExpressions;

namespace MngAdmin.Infrastructure.Backup.Services;

/// <summary>
/// MinIO backup storage service implementation
/// </summary>
public class MinioBackupService : IMinioBackupService
{
    private readonly ILogger<MinioBackupService> _logger;
    private readonly IMinioClient _minioClient;

    public MinioBackupService(ILogger<MinioBackupService> logger, IOptions<MngAdminSettings> settings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        var minioSettings = settings.Value.MinIO ?? throw new ArgumentNullException(nameof(settings));
        
        _minioClient = new MinioClient()
            .WithEndpoint(minioSettings.Endpoint)
            .WithCredentials(minioSettings.AccessKey, minioSettings.SecretKey)
            .WithSSL(minioSettings.UseSSL)
            .Build();
            
        _logger.LogInformation("MinIO backup service initialized with endpoint: {Endpoint}", minioSettings.Endpoint);
    }

    public async Task<bool> UploadBackupAsync(string bucketName, string objectPath, Stream backupStream, string contentType, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Uploading backup to MinIO: {BucketName}/{ObjectPath}", bucketName, objectPath);

            // Ensure bucket exists - this is critical for domain backups
            var bucketExists = await BucketExistsAsync(bucketName, cancellationToken);
            if (!bucketExists)
            {
                _logger.LogInformation("Bucket does not exist, creating: {BucketName}", bucketName);
                await CreateBucketAsync(bucketName, cancellationToken);
                
                // Verify bucket was created
                bucketExists = await BucketExistsAsync(bucketName, cancellationToken);
                if (!bucketExists)
                {
                    _logger.LogError("Failed to create bucket: {BucketName}. Upload will fail.", bucketName);
                    return false;
                }
                _logger.LogInformation("Bucket created and verified: {BucketName}", bucketName);
            }
            else
            {
                _logger.LogInformation("Bucket already exists: {BucketName}", bucketName);
            }

            Stream streamToUse = backupStream;
            long streamLength;
            bool shouldDisposeStream = false;

            // Get stream length
            if (backupStream.CanSeek && backupStream.Length >= 0)
            {
                streamLength = backupStream.Length - backupStream.Position;
            }
            else
            {
                var memoryStream = new MemoryStream();
                await backupStream.CopyToAsync(memoryStream, cancellationToken);
                memoryStream.Seek(0, SeekOrigin.Begin);
                streamLength = memoryStream.Length;
                streamToUse = memoryStream;
                shouldDisposeStream = true;
            }

            try
            {
                var putObjectArgs = new PutObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectPath)
                    .WithStreamData(streamToUse)
                    .WithObjectSize(streamLength)
                    .WithContentType(contentType);

                await _minioClient.PutObjectAsync(putObjectArgs, cancellationToken);

                _logger.LogInformation("Backup uploaded successfully: {BucketName}/{ObjectPath} ({Size} bytes)", 
                    bucketName, objectPath, streamLength);
                return true;
            }
            finally
            {
                if (shouldDisposeStream && streamToUse != backupStream)
                {
                    streamToUse.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload backup to MinIO: {BucketName}/{ObjectPath}", bucketName, objectPath);
            return false;
        }
    }

    public async Task<List<BackupFileInfo>> ListBackupsAsync(string bucketName, string backupPath, string databaseName, CancellationToken cancellationToken = default)
    {
        // Note: MinIO 7.0 API is different, this method is simplified
        // For now, we'll rely on BackupStatus collection for listing backups
        // This method can be enhanced later when MinIO API is properly integrated
        _logger.LogWarning("ListBackupsAsync is not fully implemented - using BackupStatus collection instead");
        return new List<BackupFileInfo>();
    }

    public async Task<bool> DeleteBackupAsync(string bucketName, string objectPath, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Deleting backup from MinIO: {BucketName}/{ObjectPath}", bucketName, objectPath);

            var removeObjectArgs = new RemoveObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectPath);

            await _minioClient.RemoveObjectAsync(removeObjectArgs, cancellationToken);

            _logger.LogInformation("Backup deleted successfully: {BucketName}/{ObjectPath}", bucketName, objectPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete backup from MinIO: {BucketName}/{ObjectPath}", bucketName, objectPath);
            return false;
        }
    }

    public async Task<BackupFileInfo?> GetBackupInfoAsync(string bucketName, string objectPath, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting backup info: {BucketName}/{ObjectPath}", bucketName, objectPath);

            var statObjectArgs = new StatObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectPath);

            var stat = await _minioClient.StatObjectAsync(statObjectArgs, cancellationToken);

            var fileName = System.IO.Path.GetFileName(objectPath);
            var match = Regex.Match(fileName, @"^(.+)_(\d{8})_(\d{6})\.(.+)$");
            
            if (match.Success)
            {
                var databaseName = match.Groups[1].Value;
                var dateStr = match.Groups[2].Value;
                var timeStr = match.Groups[3].Value;
                
                if (DateTime.TryParseExact($"{dateStr} {timeStr}", "yyyyMMdd HHmmss", null, 
                    System.Globalization.DateTimeStyles.AssumeUniversal, out var backupDate))
                {
                    return new BackupFileInfo
                    {
                        ObjectPath = objectPath,
                        SizeBytes = (long)stat.Size,
                        LastModified = stat.LastModified,
                        DatabaseName = databaseName,
                        BackupDate = backupDate
                    };
                }
            }

            return null;
        }
        catch (Minio.Exceptions.ObjectNotFoundException)
        {
            _logger.LogInformation("Backup not found: {BucketName}/{ObjectPath}", bucketName, objectPath);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get backup info: {BucketName}/{ObjectPath}", bucketName, objectPath);
            return null;
        }
    }

    private async Task<bool> BucketExistsAsync(string bucketName, CancellationToken cancellationToken)
    {
        try
        {
            var bucketExistsArgs = new BucketExistsArgs()
                .WithBucket(bucketName);

            return await _minioClient.BucketExistsAsync(bucketExistsArgs, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if bucket exists: {BucketName}. Error: {Error}", bucketName, ex.Message);
            return false;
        }
    }

    private async Task CreateBucketAsync(string bucketName, CancellationToken cancellationToken)
    {
        try
        {
            // Check if bucket already exists first
            var exists = await BucketExistsAsync(bucketName, cancellationToken);
            if (exists)
            {
                _logger.LogInformation("Bucket already exists: {BucketName}", bucketName);
                return;
            }

            _logger.LogInformation("Creating MinIO bucket: {BucketName}", bucketName);
            var makeBucketArgs = new MakeBucketArgs()
                .WithBucket(bucketName);

            await _minioClient.MakeBucketAsync(makeBucketArgs, cancellationToken);
            _logger.LogInformation("Bucket created successfully: {BucketName}", bucketName);
        }
        catch (Exception ex)
        {
            // Check if error indicates bucket already exists
            if (ex.Message.Contains("BucketAlreadyExists", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("bucket already exists", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("BucketAlreadyOwnedByYou", StringComparison.OrdinalIgnoreCase) ||
                ex.GetType().Name.Contains("BucketAlready", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Bucket already exists: {BucketName}", bucketName);
                return; // Bucket exists, that's fine
            }
            
            _logger.LogError(ex, "Failed to create bucket: {BucketName}. Error: {Error}", bucketName, ex.Message);
            throw; // Re-throw to let caller handle the error
        }
    }
}
