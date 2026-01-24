using System.Collections.Concurrent;
using MngDataGateway.Application.Configuration;
using MngDataGateway.Application.DTOs.Files;
using MngDataGateway.Application.Services.Files;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MngDataGateway.Infrastructure.Services.Files;

/// <summary>
/// File processing pipeline implementation
/// Orchestrates: validation → compression → encryption → upload
/// </summary>
public class FileProcessingPipeline : IFileProcessingPipeline
{
    private readonly ILogger<FileProcessingPipeline> _logger;
    private readonly IFileFieldValidator _validator;
    private readonly IFileCompressionService _compressionService;
    private readonly IFileEncryptionService _encryptionService;
    private readonly IMinIOFileService _minioService;
    private readonly FileStorageSettings _settings;

    public FileProcessingPipeline(
        ILogger<FileProcessingPipeline> logger,
        IFileFieldValidator validator,
        IFileCompressionService compressionService,
        IFileEncryptionService encryptionService,
        IMinIOFileService minioService,
        IOptions<MngDataGatewaySettings> options)
    {
        _logger = logger;
        _validator = validator;
        _compressionService = compressionService;
        _encryptionService = encryptionService;
        _minioService = minioService;
        _settings = options.Value.FileStorage;
    }

    /// <summary>
    /// Processes file upload end-to-end
    /// </summary>
    public async Task<FileProcessingResult> ProcessFileUploadAsync(
        FileUploadRequestDto request,
        string domain,
        string datasetName,
        string recordId,
        FileProcessingOptionsDto options,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting file processing pipeline: domain={Domain}, dataset={Dataset}, record={Record}",
            domain, datasetName, recordId);

        try
        {
            // Step 1: Validate base64 input
            _logger.LogDebug("Step 1: Decoding base64 content");
            byte[] decodedData = _validator.DecodeBase64(request.Content);

            // Step 2: Validate file
            _logger.LogDebug("Step 2: Validating file (size, type, extension)");
            ValidateFile(decodedData, request, options);

            // Step 3: Detect MIME type
            _logger.LogDebug("Step 3: Detecting MIME type from magic bytes");
            string mimeType = _validator.DetectMimeType(decodedData);
            string extension = _validator.GetExtensionFromMimeType(mimeType);

            // Step 4: Validate folder path
            _logger.LogDebug("Step 4: Validating folder path");
            var folderValidation = _validator.ValidateFolderPath(request.Folder);
            if (!folderValidation.IsValid)
                throw new ArgumentException($"Invalid folder path: {folderValidation.ErrorMessage}");

            // Step 5: Compression (optional, non-fatal on failure)
            _logger.LogDebug("Step 5: Processing compression (enabled={UseCompression})", 
                request.UseCompression);
            byte[] processedData = decodedData;
            bool isCompressed = false;
            double compressionRatio = 1.0;

            if (request.UseCompression)
            {
                var compressionResult = await _compressionService.CompressAsync(decodedData);
                processedData = compressionResult.Data;
                isCompressed = compressionResult.IsCompressed;
                compressionRatio = compressionResult.CompressionRatio;

                if (!isCompressed && compressionResult.ErrorMessage != null)
                {
                    _logger.LogWarning("Compression skipped (non-fatal): {Reason}",
                        compressionResult.ErrorMessage);
                }
            }

            // Step 6: Encryption (optional, fatal on failure)
            _logger.LogDebug("Step 6: Processing encryption (enabled={UseEncryption})",
                request.UseEncryption);
            bool isEncrypted = false;

            if (request.UseEncryption)
            {
                processedData = await _encryptionService.EncryptAsync(processedData);
                isEncrypted = true;
            }

            // Step 7: Build object path
            _logger.LogDebug("Step 7: Building MinIO object path");
            string fileId = Guid.NewGuid().ToString();
            string bucketName = $"mng-{domain}";
            string objectPath = BuildObjectPath(domain, datasetName, recordId, 
                folderValidation.NormalizedPath, fileId, extension);

            _logger.LogDebug("Object path: {ObjectPath}", objectPath);

            // Step 8: Build metadata
            _logger.LogDebug("Step 8: Building metadata for MinIO headers");
            var metadata = BuildMetadata(
                request, domain, datasetName, recordId,
                mimeType, decodedData.Length, isCompressed, isEncrypted);

            // Step 9: Upload to MinIO
            _logger.LogDebug("Step 9: Uploading to MinIO (bucket={Bucket})", bucketName);
            var uploadResult = await _minioService.UploadFileAsync(
                bucketName,
                objectPath,
                processedData,
                metadata,
                cancellationToken);

            // Step 10: Build and return result
            _logger.LogInformation(
                "File processing completed successfully. ObjectPath={ObjectPath}, " +
                "OriginalSize={OriginalSize}, StoredSize={StoredSize}, " +
                "Compressed={IsCompressed}, Encrypted={IsEncrypted}",
                objectPath, decodedData.Length, processedData.Length,
                isCompressed, isEncrypted);

            return new FileProcessingResult
            {
                FilePath = objectPath,
                OriginalFileName = ExtractFileName(request.Content),
                OriginalFileSize = decodedData.Length,
                MimeType = mimeType,
                Extension = extension,
                IsCompressed = isCompressed,
                IsEncrypted = isEncrypted,
                CompressionRatio = compressionRatio,
                StoredFileSize = processedData.Length,
                UploadedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "File processing pipeline failed: domain={Domain}, dataset={Dataset}, record={Record}",
                domain, datasetName, recordId);
            throw;
        }
    }

    /// <summary>
    /// Processes file download
    /// Retrieves, decrypts, decompresses
    /// </summary>
    public async Task<byte[]> ProcessFileDownloadAsync(
        string domain,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting file download processing: filePath={FilePath}", filePath);

        try
        {
            // Extract bucket name from path
            string bucketName = $"mng-{domain}";

            // Step 1: Download from MinIO
            _logger.LogDebug("Step 1: Downloading from MinIO");
            byte[] fileData = await _minioService.DownloadFileAsync(
                bucketName, filePath, cancellationToken);

            // Step 2: Get metadata
            _logger.LogDebug("Step 2: Retrieving metadata");
            var metadata = await _minioService.GetFileMetadataAsync(
                bucketName, filePath, cancellationToken);

            bool isEncrypted = metadata.TryGetValue("x-amz-meta-is-encrypted", out var encryptedStr) &&
                bool.TryParse(encryptedStr, out var encrypted) && encrypted;

            bool isCompressed = metadata.TryGetValue("x-amz-meta-is-zipped", out var zippedStr) &&
                bool.TryParse(zippedStr, out var zipped) && zipped;

            // Step 3: Decrypt (if needed)
            if (isEncrypted)
            {
                _logger.LogDebug("Step 3: Decrypting");
                fileData = await _encryptionService.DecryptAsync(fileData);
            }

            // Step 4: Decompress (if needed)
            if (isCompressed)
            {
                _logger.LogDebug("Step 4: Decompressing");
                fileData = await _compressionService.DecompressAsync(fileData);
            }

            _logger.LogInformation(
                "File download processing completed: filePath={FilePath}, " +
                "FinalSize={Size}, Decrypted={IsEncrypted}, Decompressed={IsCompressed}",
                filePath, fileData.Length, isEncrypted, isCompressed);

            return fileData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File download processing failed: filePath={FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Validates file against options
    /// </summary>
    private void ValidateFile(
        byte[] fileData,
        FileUploadRequestDto request,
        FileProcessingOptionsDto options)
    {
        // Check size
        if (!_validator.ValidateFileSize(fileData.Length, options.MaxFileSize))
            throw new ArgumentException(
                $"File size {fileData.Length} bytes exceeds maximum {options.MaxFileSize} bytes");

        // Detect MIME type and extension
        string mimeType = _validator.DetectMimeType(fileData);
        string extension = _validator.GetExtensionFromMimeType(mimeType);

        // Check extension (if restrictions exist)
        if (options.AllowedExtensions != null && options.AllowedExtensions.Count > 0)
        {
            if (!_validator.ValidateExtension(extension, options.AllowedExtensions))
                throw new ArgumentException(
                    $"File type {extension} not allowed. Allowed types: {string.Join(", ", options.AllowedExtensions)}");
        }

        _logger.LogDebug("File validation passed: extension={Extension}, mimeType={MimeType}, size={Size}",
            extension, mimeType, fileData.Length);
    }

    /// <summary>
    /// Builds MinIO object path
    /// Format: /mng-{domain}/data/{datasetName}/{recordId}/{folder?}/{fileId}.{ext}
    /// </summary>
    private string BuildObjectPath(
        string domain,
        string datasetName,
        string recordId,
        string? folder,
        string fileId,
        string extension)
    {
        var pathParts = new List<string>
        {
            $"mng-{domain}",
            "data",
            datasetName,
            recordId
        };

        if (!string.IsNullOrEmpty(folder))
        {
            pathParts.Add(folder);
        }

        pathParts.Add($"{fileId}{extension}");

        return "/" + string.Join("/", pathParts);
    }

    /// <summary>
    /// Builds metadata dictionary for MinIO headers
    /// </summary>
    private Dictionary<string, string> BuildMetadata(
        FileUploadRequestDto request,
        string domain,
        string datasetName,
        string recordId,
        string mimeType,
        long fileSize,
        bool isCompressed,
        bool isEncrypted)
    {
        var metadata = new Dictionary<string, string>
        {
            // File information
            ["x-amz-meta-original-filename"] = ExtractFileName(request.Content),
            ["x-amz-meta-file-size"] = fileSize.ToString(),
            ["x-amz-meta-mime-type"] = mimeType,

            // Timestamps
            ["x-amz-meta-created-at"] = DateTime.UtcNow.ToString("O"),
            ["x-amz-meta-uploaded-at"] = DateTime.UtcNow.ToString("O"),

            // Context
            ["x-amz-meta-domain-name"] = domain,
            ["x-amz-meta-dataset-name"] = datasetName,
            ["x-amz-meta-record-id"] = recordId,

            // Processing flags
            ["x-amz-meta-is-zipped"] = isCompressed.ToString().ToLowerInvariant(),
            ["x-amz-meta-is-encrypted"] = isEncrypted.ToString().ToLowerInvariant(),

            // Encryption config
            ["x-amz-meta-encryption-config"] = isEncrypted
                ? @"{""algorithm"":""AES-256-GCM"",""keyDerivation"":""PBKDF2""}"
                : ""
        };

        return metadata;
    }

    /// <summary>
    /// Extracts original filename from base64 content
    /// Uses timestamp as fallback if not available
    /// </summary>
    private string ExtractFileName(string base64Content)
    {
        // Try to determine file type from content
        // Fallback to timestamp-based name
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        return $"file_{timestamp}";
    }
}
