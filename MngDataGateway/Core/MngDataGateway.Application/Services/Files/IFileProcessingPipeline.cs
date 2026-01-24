namespace MngDataGateway.Application.Services.Files;

/// <summary>
/// File processing pipeline service
/// Coordinates all file processing steps: validation, compression, encryption, upload
/// </summary>
public interface IFileProcessingPipeline
{
    /// <summary>
    /// Processes a file upload request end-to-end
    /// </summary>
    /// <param name="request">File upload request with base64 content</param>
    /// <param name="domain">Domain name for bucket selection</param>
    /// <param name="datasetName">Dataset name for path construction</param>
    /// <param name="recordId">Record ID for path construction</param>
    /// <param name="options">Processing options (max size, allowed extensions, etc.)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processing result with file path and metadata</returns>
    Task<FileProcessingResult> ProcessFileUploadAsync(
        FileUploadRequestDto request,
        string domain,
        string datasetName,
        string recordId,
        FileProcessingOptionsDto options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes file download request
    /// Retrieves file from MinIO, decrypts, decompresses
    /// </summary>
    Task<byte[]> ProcessFileDownloadAsync(
        string domain,
        string filePath,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// File processing result
/// Contains file path and metadata after successful processing
/// </summary>
public class FileProcessingResult
{
    /// <summary>
    /// File path in MinIO (stored in database)
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Original filename from request
    /// </summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes (original, before compression)
    /// </summary>
    public long OriginalFileSize { get; set; }

    /// <summary>
    /// MIME type detected from magic bytes
    /// </summary>
    public string MimeType { get; set; } = string.Empty;

    /// <summary>
    /// File extension
    /// </summary>
    public string Extension { get; set; } = string.Empty;

    /// <summary>
    /// Whether file was compressed
    /// </summary>
    public bool IsCompressed { get; set; }

    /// <summary>
    /// Whether file was encrypted
    /// </summary>
    public bool IsEncrypted { get; set; }

    /// <summary>
    /// Compression ratio (if compressed)
    /// </summary>
    public double CompressionRatio { get; set; } = 1.0;

    /// <summary>
    /// Upload timestamp
    /// </summary>
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Actual size stored in MinIO (after compression/encryption)
    /// </summary>
    public long StoredFileSize { get; set; }
}
