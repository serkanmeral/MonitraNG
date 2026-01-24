using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;
using MngDataGateway.Application.Configuration;
using MngDataGateway.Application.Services.Files;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MngDataGateway.Infrastructure.Services.Files;

/// <summary>
/// MinIO file storage service implementation
/// Handles file upload, download, and metadata operations with retry mechanism
/// </summary>
public class MinIOFileService : IMinIOFileService
{
    private readonly ILogger<MinIOFileService> _logger;
    private readonly IMinioClient _minioClient;
    private readonly FileStorageSettings _settings;

    public MinIOFileService(
        ILogger<MinIOFileService> logger,
        IMinioClient minioClient,
        IOptions<MngDataGatewaySettings> options)
    {
        _logger = logger;
        _minioClient = minioClient;
        _settings = options.Value.FileStorage;

        _logger.LogInformation("MinIOFileService initialized with endpoint: {Endpoint}",
            _settings.Minio.Endpoint);
    }

    /// <summary>
    /// Uploads file to MinIO with metadata and retry mechanism
    /// </summary>
    public async Task<FileUploadResult> UploadFileAsync(
        string bucketName,
        string objectPath,
        byte[] fileContent,
        Dictionary<string, string> metadata,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bucketName))
            throw new ArgumentException("Bucket name cannot be empty", nameof(bucketName));

        if (string.IsNullOrWhiteSpace(objectPath))
            throw new ArgumentException("Object path cannot be empty", nameof(objectPath));

        if (fileContent == null || fileContent.Length == 0)
            throw new ArgumentException("File content cannot be empty", nameof(fileContent));

        // Ensure bucket exists
        await EnsureBucketExistsAsync(bucketName, cancellationToken);

        // Retry logic
        var maxAttempts = _settings.Retry.MaxAttempts;
        var backoffDelays = _settings.Retry.BackoffDelayMs;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                // Apply backoff delay if not first attempt
                if (attempt > 1 && attempt - 1 < backoffDelays.Count)
                {
                    var delayMs = backoffDelays[attempt - 1];
                    if (delayMs > 0)
                    {
                        _logger.LogInformation("Retry attempt {Attempt}/{MaxAttempts} - waiting {DelayMs}ms",
                            attempt, maxAttempts, delayMs);
                        await Task.Delay(delayMs, cancellationToken);
                    }
                }

                // Upload file
                using var stream = new MemoryStream(fileContent);

                var putArgs = new PutObjectArgs()
                    .WithBucket(bucketName)
                    .WithObject(objectPath)
                    .WithStreamData(stream)
                    .WithObjectSize(stream.Length)
                    .WithContentType(metadata.ContainsKey("x-amz-meta-mime-type")
                        ? metadata["x-amz-meta-mime-type"]
                        : "application/octet-stream");

                // Add metadata headers
                if (metadata != null && metadata.Count > 0)
                {
                    var headers = new Dictionary<string, string>(metadata);
                    putArgs = putArgs.WithHeaders(headers);
                }

                var response = await _minioClient.PutObjectAsync(putArgs, cancellationToken);

                _logger.LogInformation(
                    "File uploaded successfully: {ObjectPath} ({Size} bytes) to bucket {Bucket}",
                    objectPath, fileContent.Length, bucketName);

                return new FileUploadResult
                {
                    BucketName = bucketName,
                    ObjectPath = objectPath,
                    FileSize = fileContent.Length,
                    UploadedAt = DateTime.UtcNow
                };
            }
            catch (HttpRequestException ex) when (attempt < maxAttempts)
            {
                _logger.LogWarning(ex,
                    "Upload attempt {Attempt}/{MaxAttempts} failed: {Message}. Retrying...",
                    attempt, maxAttempts, ex.Message);
                continue;
            }
            catch (Exception ex) when (attempt < maxAttempts && IsRetryableError(ex))
            {
                _logger.LogWarning(ex,
                    "Retryable error on attempt {Attempt}/{MaxAttempts}: {Message}. Retrying...",
                    attempt, maxAttempts, ex.Message);
                continue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Upload failed on attempt {Attempt}/{MaxAttempts}: {Message}",
                    attempt, maxAttempts, ex.Message);
                throw new InvalidOperationException(
                    $"File upload failed after {attempt} attempts", ex);
            }
        }

        throw new InvalidOperationException(
            $"File upload failed after {maxAttempts} attempts");
    }

    /// <summary>
    /// Downloads file from MinIO
    /// </summary>
    public async Task<byte[]> DownloadFileAsync(
        string bucketName,
        string objectPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bucketName))
            throw new ArgumentException("Bucket name cannot be empty", nameof(bucketName));

        if (string.IsNullOrWhiteSpace(objectPath))
            throw new ArgumentException("Object path cannot be empty", nameof(objectPath));

        try
        {
            using var memoryStream = new MemoryStream();

            var getArgs = new GetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectPath)
                .WithCallbackStream(stream => stream.CopyTo(memoryStream));

            await _minioClient.GetObjectAsync(getArgs, cancellationToken);

            var fileContent = memoryStream.ToArray();

            _logger.LogInformation("File downloaded successfully: {ObjectPath} ({Size} bytes) from bucket {Bucket}",
                objectPath, fileContent.Length, bucketName);

            return fileContent;
        }
        catch (ObjectNotFoundException ex)
        {
            _logger.LogError(ex, "File not found: {ObjectPath} in bucket {Bucket}",
                objectPath, bucketName);
            throw new FileNotFoundException($"File not found: {objectPath}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Download failed: {ObjectPath} from bucket {Bucket}",
                objectPath, bucketName);
            throw new InvalidOperationException(
                $"Failed to download file: {objectPath}", ex);
        }
    }

    /// <summary>
    /// Gets file metadata from MinIO headers
    /// </summary>
    public async Task<Dictionary<string, string>> GetFileMetadataAsync(
        string bucketName,
        string objectPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bucketName))
            throw new ArgumentException("Bucket name cannot be empty", nameof(bucketName));

        if (string.IsNullOrWhiteSpace(objectPath))
            throw new ArgumentException("Object path cannot be empty", nameof(objectPath));

        try
        {
            var statArgs = new StatObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectPath);

            var stat = await _minioClient.StatObjectAsync(statArgs, cancellationToken);

            var metadata = new Dictionary<string, string>();

            // Add standard metadata
            metadata["content-type"] = stat.ContentType ?? "application/octet-stream";
            metadata["content-length"] = stat.Size.ToString();
            metadata["last-modified"] = stat.LastModified.ToString("O");

            // Add custom metadata from headers
            // Normalize keys to lowercase for consistent lookup
            // MinIO may return keys in different cases (X-Amz-Meta-* vs x-amz-meta-*)
            if (stat.MetaData != null)
            {
                foreach (var kvp in stat.MetaData)
                {
                    var normalizedKey = kvp.Key.ToLowerInvariant();
                    metadata[normalizedKey] = kvp.Value;
                    _logger.LogDebug("Metadata entry: OriginalKey={OriginalKey}, NormalizedKey={NormalizedKey}, Value={Value}",
                        kvp.Key, normalizedKey, kvp.Value);
                }
            }

            _logger.LogDebug("Retrieved metadata for {ObjectPath} from bucket {Bucket}: {MetadataCount} entries. Keys: {Keys}",
                objectPath, bucketName, metadata.Count, string.Join(", ", metadata.Keys));

            return metadata;
        }
        catch (ObjectNotFoundException ex)
        {
            _logger.LogError(ex, "File not found: {ObjectPath} in bucket {Bucket}",
                objectPath, bucketName);
            throw new FileNotFoundException($"File not found: {objectPath}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get metadata: {ObjectPath} from bucket {Bucket}",
                objectPath, bucketName);
            throw new InvalidOperationException(
                $"Failed to get file metadata: {objectPath}", ex);
        }
    }

    /// <summary>
    /// Deletes file from MinIO
    /// </summary>
    public async Task DeleteFileAsync(
        string bucketName,
        string objectPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bucketName))
            throw new ArgumentException("Bucket name cannot be empty", nameof(bucketName));

        if (string.IsNullOrWhiteSpace(objectPath))
            throw new ArgumentException("Object path cannot be empty", nameof(objectPath));

        try
        {
            var rmArgs = new RemoveObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectPath);

            await _minioClient.RemoveObjectAsync(rmArgs, cancellationToken);

            _logger.LogInformation("File deleted successfully: {ObjectPath} from bucket {Bucket}",
                objectPath, bucketName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file: {ObjectPath} from bucket {Bucket}",
                objectPath, bucketName);
            throw new InvalidOperationException(
                $"Failed to delete file: {objectPath}", ex);
        }
    }

    /// <summary>
    /// Checks if bucket exists, creates if not
    /// </summary>
    public async Task EnsureBucketExistsAsync(
        string bucketName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bucketName))
            throw new ArgumentException("Bucket name cannot be empty", nameof(bucketName));

        try
        {
            var existsArgs = new BucketExistsArgs()
                .WithBucket(bucketName);

            var exists = await _minioClient.BucketExistsAsync(existsArgs, cancellationToken);

            if (!exists)
            {
                _logger.LogInformation("Bucket {BucketName} does not exist, creating...", bucketName);

                var makeBucketArgs = new MakeBucketArgs()
                    .WithBucket(bucketName);

                await _minioClient.MakeBucketAsync(makeBucketArgs, cancellationToken);

                _logger.LogInformation("Bucket {BucketName} created successfully", bucketName);
            }
            else
            {
                _logger.LogDebug("Bucket {BucketName} exists", bucketName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure bucket {BucketName} exists", bucketName);
            throw new InvalidOperationException(
                $"Failed to ensure bucket exists: {bucketName}", ex);
        }
    }

    /// <summary>
    /// Generates presigned URL for direct download (optional, not used in Phase 1)
    /// </summary>
    public async Task<string> GeneratePresignedUrlAsync(
        string bucketName,
        string objectPath,
        int expirationSeconds = 3600,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(bucketName))
            throw new ArgumentException("Bucket name cannot be empty", nameof(bucketName));

        if (string.IsNullOrWhiteSpace(objectPath))
            throw new ArgumentException("Object path cannot be empty", nameof(objectPath));

        try
        {
            var presignedArgs = new PresignedGetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectPath)
                .WithExpiry(expirationSeconds);

            var url = await _minioClient.PresignedGetObjectAsync(presignedArgs);

            _logger.LogDebug("Generated presigned URL for {ObjectPath}: expires in {Seconds}s",
                objectPath, expirationSeconds);

            return url;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate presigned URL for {ObjectPath}",
                objectPath);
            throw new InvalidOperationException(
                $"Failed to generate presigned URL: {objectPath}", ex);
        }
    }

    /// <summary>
    /// Determines if an error is retryable
    /// </summary>
    private bool IsRetryableError(Exception ex)
    {
        // Retryable errors: timeouts, connection issues, 503 Service Unavailable, etc.
        if (ex is TimeoutException or OperationCanceledException)
            return true;

        var message = ex.Message.ToLowerInvariant();
        if (message.Contains("timeout") || message.Contains("connection") ||
            message.Contains("503") || message.Contains("service unavailable") ||
            message.Contains("temporarily unavailable"))
            return true;

        // Check inner exceptions
        if (ex.InnerException != null)
            return IsRetryableError(ex.InnerException);

        return false;
    }
}
