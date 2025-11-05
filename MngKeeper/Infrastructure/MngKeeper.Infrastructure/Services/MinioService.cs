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
}

