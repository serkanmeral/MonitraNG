using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using MngKeeper.Application.Interfaces;
using Minio;
using Minio.DataModel.Args;
using System.Text;

namespace MngKeeper.Infrastructure.Services;

public class MinioService : IMinioService
{
    private readonly ILogger<MinioService> _logger;
    private readonly IMinioClient _minioClient;

    public MinioService(ILogger<MinioService> logger, IConfiguration configuration)
    {
        _logger = logger;
        
        var endpoint = configuration["MngKeeperSettings:MinIO:Endpoint"] ?? "localhost:9000";
        var accessKey = configuration["MngKeeperSettings:MinIO:AccessKey"] ?? "admin";
        var secretKey = configuration["MngKeeperSettings:MinIO:SecretKey"] ?? "admin123";
        var useSSL = bool.Parse(configuration["MngKeeperSettings:MinIO:UseSSL"] ?? "false");

        _minioClient = new MinioClient()
            .WithEndpoint(endpoint)
            .WithCredentials(accessKey, secretKey)
            .WithSSL(useSSL)
            .Build();
            
        _logger.LogInformation("MinIO client initialized with endpoint: {Endpoint}", endpoint);
    }

    public async Task<bool> CreateBucketAsync(string bucketName, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Creating MinIO bucket: {BucketName}", bucketName);

            // Check if bucket already exists
            var exists = await BucketExistsAsync(bucketName, cancellationToken);
            if (exists)
            {
                _logger.LogInformation("Bucket already exists: {BucketName}", bucketName);
                return true;
            }

            // Create bucket
            var makeBucketArgs = new MakeBucketArgs()
                .WithBucket(bucketName);

            await _minioClient.MakeBucketAsync(makeBucketArgs, cancellationToken);

            _logger.LogInformation("MinIO bucket created successfully: {BucketName}", bucketName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create MinIO bucket: {BucketName}", bucketName);
            return false;
        }
    }

    public async Task<bool> BucketExistsAsync(string bucketName, CancellationToken cancellationToken = default)
    {
        try
        {
            var bucketExistsArgs = new BucketExistsArgs()
                .WithBucket(bucketName);

            return await _minioClient.BucketExistsAsync(bucketExistsArgs, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if bucket exists: {BucketName}", bucketName);
            return false;
        }
    }

    public async Task<bool> DeleteBucketAsync(string bucketName, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Deleting MinIO bucket: {BucketName}", bucketName);

            var removeBucketArgs = new RemoveBucketArgs()
                .WithBucket(bucketName);

            await _minioClient.RemoveBucketAsync(removeBucketArgs, cancellationToken);

            _logger.LogInformation("MinIO bucket deleted successfully: {BucketName}", bucketName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete MinIO bucket: {BucketName}", bucketName);
            return false;
        }
    }

    public async Task<bool> CreateFolderStructureAsync(string bucketName, string[] folders, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Creating folder structure in bucket: {BucketName}", bucketName);

            foreach (var folder in folders)
            {
                // MinIO doesn't have real folders, we create empty objects with trailing /
                var folderPath = folder.EndsWith('/') ? folder : $"{folder}/";
                var objectName = $"{folderPath}.keep"; // Create a placeholder file

                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(""));
                var putObjectArgs = new PutObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectName)
                    .WithStreamData(stream)
                    .WithObjectSize(0);

                await _minioClient.PutObjectAsync(putObjectArgs, cancellationToken);
                
                _logger.LogInformation("Created folder: {BucketName}/{FolderPath}", bucketName, folderPath);
            }

            _logger.LogInformation("Folder structure created successfully in bucket: {BucketName}", bucketName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create folder structure in bucket: {BucketName}", bucketName);
            return false;
        }
    }

    public async Task<bool> SetBucketPolicyAsync(string bucketName, string policy, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Setting bucket policy: {BucketName}", bucketName);

            var setBucketPolicyArgs = new SetPolicyArgs()
                .WithBucket(bucketName)
                .WithPolicy(policy);

            await _minioClient.SetPolicyAsync(setBucketPolicyArgs, cancellationToken);

            _logger.LogInformation("Bucket policy set successfully: {BucketName}", bucketName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set bucket policy: {BucketName}", bucketName);
            return false;
        }
    }

    public async Task<Stream?> GetObjectAsync(string bucketName, string objectName, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Getting object from MinIO: {BucketName}/{ObjectName}", bucketName, objectName);

            // Check if bucket exists
            var bucketExists = await BucketExistsAsync(bucketName, cancellationToken);
            if (!bucketExists)
            {
                _logger.LogWarning("Bucket does not exist: {BucketName}", bucketName);
                return null;
            }

            var memoryStream = new MemoryStream();
            
            var getObjectArgs = new GetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithCallbackStream(stream => stream.CopyTo(memoryStream));

            await _minioClient.GetObjectAsync(getObjectArgs, cancellationToken);
            
            memoryStream.Seek(0, SeekOrigin.Begin);
            
            _logger.LogInformation("Object retrieved successfully: {BucketName}/{ObjectName}", bucketName, objectName);
            return memoryStream;
        }
        catch (Minio.Exceptions.ObjectNotFoundException)
        {
            _logger.LogInformation("Object not found: {BucketName}/{ObjectName}", bucketName, objectName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get object from MinIO: {BucketName}/{ObjectName}", bucketName, objectName);
            return null;
        }
    }

    public async Task<bool> PutObjectAsync(string bucketName, string objectName, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Putting object to MinIO: {BucketName}/{ObjectName}", bucketName, objectName);

            // Ensure bucket exists
            var bucketExists = await BucketExistsAsync(bucketName, cancellationToken);
            if (!bucketExists)
            {
                _logger.LogInformation("Bucket does not exist, creating: {BucketName}", bucketName);
                await CreateBucketAsync(bucketName, cancellationToken);
            }

            Stream streamToUse = content;
            long streamLength;
            bool shouldDisposeStream = false;

            // Get stream length (if stream supports it)
            if (content.CanSeek && content.Length >= 0)
            {
                streamLength = content.Length - content.Position;
            }
            else
            {
                // If stream doesn't support Length/Seek, copy to MemoryStream
                var memoryStream = new MemoryStream();
                await content.CopyToAsync(memoryStream, cancellationToken);
                memoryStream.Seek(0, SeekOrigin.Begin);
                streamLength = memoryStream.Length;
                streamToUse = memoryStream;
                shouldDisposeStream = true;
            }

            try
            {
                var putObjectArgs = new PutObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectName)
                    .WithStreamData(streamToUse)
                    .WithObjectSize(streamLength)
                    .WithContentType(contentType);

                await _minioClient.PutObjectAsync(putObjectArgs, cancellationToken);

                _logger.LogInformation("Object uploaded successfully: {BucketName}/{ObjectName}", bucketName, objectName);
                return true;
            }
            finally
            {
                if (shouldDisposeStream && streamToUse != content)
                {
                    streamToUse.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to put object to MinIO: {BucketName}/{ObjectName}", bucketName, objectName);
            return false;
        }
    }

    public async Task<bool> RemoveObjectAsync(string bucketName, string objectName, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Removing object from MinIO: {BucketName}/{ObjectName}", bucketName, objectName);

            // Check if bucket exists
            var bucketExists = await BucketExistsAsync(bucketName, cancellationToken);
            if (!bucketExists)
            {
                _logger.LogWarning("Bucket does not exist: {BucketName}", bucketName);
                return false;
            }

            var removeObjectArgs = new RemoveObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName);

            await _minioClient.RemoveObjectAsync(removeObjectArgs, cancellationToken);

            _logger.LogInformation("Object removed successfully: {BucketName}/{ObjectName}", bucketName, objectName);
            return true;
        }
        catch (Minio.Exceptions.ObjectNotFoundException)
        {
            _logger.LogInformation("Object not found (may have been already deleted): {BucketName}/{ObjectName}", bucketName, objectName);
            return true; // Object doesn't exist, consider it success (idempotent)
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove object from MinIO: {BucketName}/{ObjectName}", bucketName, objectName);
            return false;
        }
    }
}

