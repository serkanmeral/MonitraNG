namespace MngDataGateway.Application.Services.Files;

/// <summary>
/// MinIO file storage service interface
/// Handles file upload, download, and metadata management
/// </summary>
public interface IMinIOFileService
{
    /// <summary>
    /// Uploads file to MinIO with metadata headers and retry mechanism
    /// </summary>
    /// <param name="bucketName">Target bucket name</param>
    /// <param name="objectPath">Object path (e.g., /mng-domain/data/dataset/record/folder/file.pdf)</param>
    /// <param name="fileContent">File content bytes</param>
    /// <param name="metadata">Metadata headers to store with file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Upload result with file path and size</returns>
    /// <remarks>
    /// Implements retry mechanism: 3 attempts with exponential backoff (0s, 1s, 2s)
    /// </remarks>
    Task<FileUploadResult> UploadFileAsync(
        string bucketName,
        string objectPath,
        byte[] fileContent,
        Dictionary<string, string> metadata,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads file from MinIO
    /// </summary>
    /// <param name="bucketName">Source bucket name</param>
    /// <param name="objectPath">Object path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>File content bytes</returns>
    Task<byte[]> DownloadFileAsync(
        string bucketName,
        string objectPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets file metadata from MinIO headers
    /// </summary>
    /// <param name="bucketName">Bucket name</param>
    /// <param name="objectPath">Object path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>File metadata dictionary</returns>
    Task<Dictionary<string, string>> GetFileMetadataAsync(
        string bucketName,
        string objectPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes file from MinIO
    /// </summary>
    /// <param name="bucketName">Bucket name</param>
    /// <param name="objectPath">Object path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteFileAsync(
        string bucketName,
        string objectPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if bucket exists, creates if not
    /// </summary>
    /// <param name="bucketName">Bucket name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task EnsureBucketExistsAsync(
        string bucketName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates presigned URL for direct download (optional)
    /// </summary>
    /// <param name="bucketName">Bucket name</param>
    /// <param name="objectPath">Object path</param>
    /// <param name="expirationSeconds">URL expiration time in seconds</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Presigned URL for download</returns>
    Task<string> GeneratePresignedUrlAsync(
        string bucketName,
        string objectPath,
        int expirationSeconds = 3600,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// File upload result
/// </summary>
public class FileUploadResult
{
    public string BucketName { get; set; } = string.Empty;
    public string ObjectPath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string? PresignedUrl { get; set; }
}
