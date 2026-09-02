using System.Collections.Concurrent;
using System.IO;
using System.Linq;
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
        string userName,
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

            // Step 3: Detect MIME type (magic bytes + original file name for draw.io)
            _logger.LogDebug("Step 3: Detecting MIME type from magic bytes");
            var (mimeType, extension) = ResolveUploadType(decodedData, request.OriginalFileName);

            // Step 4: Validate folder path
            _logger.LogDebug("Step 4: Validating folder path");
            var folderValidation = _validator.ValidateFolderPath(request.Folder);
            if (!folderValidation.IsValid)
                throw new ArgumentException($"Invalid folder path: {folderValidation.ErrorMessage}");

            // Step 5: Compression (optional, non-fatal on failure)
            bool useCompression = request.UseCompression ?? options.DefaultCompression;
            _logger.LogDebug("Step 5: Processing compression (enabled={UseCompression})", 
                useCompression);
            byte[] processedData = decodedData;
            bool isCompressed = false;
            double compressionRatio = 1.0;

            if (useCompression)
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
            bool useEncryption = request.UseEncryption ?? options.DefaultEncryption;
            _logger.LogDebug("Step 6: Processing encryption (enabled={UseEncryption})",
                useEncryption);
            bool isEncrypted = false;

            if (useEncryption)
            {
                processedData = await _encryptionService.EncryptAsync(processedData);
                isEncrypted = true;
            }

            // Step 7: Build object path
            _logger.LogDebug("Step 7: Building MinIO object path");
            string fileId = Guid.NewGuid().ToString();
            string bucketName = $"mng-{domain}";
            // MinIO object path (without bucket name)
            string objectPath = BuildObjectPath(datasetName, recordId, 
                folderValidation.NormalizedPath, fileId, extension);
            // Full file path for response (with bucket name)
            string fullFilePath = $"/mng-{domain}{objectPath}";

            _logger.LogDebug("MinIO object path: {ObjectPath}, Full file path: {FullPath}", objectPath, fullFilePath);

            // Step 8: Build metadata
            _logger.LogDebug("Step 8: Building metadata for MinIO headers");
            var metadata = BuildMetadata(
                request, domain, datasetName, recordId, userName,
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

            var displayName = GetEffectiveOriginalFileName(request);
            return new FileProcessingResult
            {
                FilePath = fullFilePath,  // Return full path with bucket name for data records
                OriginalFileName = displayName,
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
            
            // Extract MinIO object path (remove bucket name prefix from full path)
            // Full path: /mng-{domain}/data/users/... -> Object path: /data/users/...
            string objectPath = filePath;
            if (filePath.StartsWith($"/mng-{domain}/"))
            {
                objectPath = filePath.Substring($"/mng-{domain}".Length);
            }
            else if (filePath.StartsWith("/mng-"))
            {
                // Extract path after /mng-{domain}/
                var pathParts = filePath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (pathParts.Length > 1)
                {
                    objectPath = "/" + string.Join("/", pathParts.Skip(1));
                }
            }

            // Step 1: Download from MinIO
            _logger.LogDebug("Step 1: Downloading from MinIO (bucket={Bucket}, object={Object})", bucketName, objectPath);
            byte[] fileData = await _minioService.DownloadFileAsync(
                bucketName, objectPath, cancellationToken);

            // Step 2: Get metadata
            _logger.LogDebug("Step 2: Retrieving metadata");
            var metadata = await _minioService.GetFileMetadataAsync(
                bucketName, objectPath, cancellationToken);

            // Log all metadata keys for debugging
            _logger.LogDebug("Metadata keys: {Keys}", string.Join(", ", metadata.Keys));

            // Parse encryption flag
            // MinIO returns metadata keys without "x-amz-meta-" prefix
            // Try both formats: "x-amz-meta-is-encrypted" and "is-encrypted"
            bool isEncrypted = false;
            string[] encryptedKeyVariants = { "x-amz-meta-is-encrypted", "is-encrypted" };
            string? encryptedKey = null;
            string? encryptedStr = null;
            
            foreach (var keyVariant in encryptedKeyVariants)
            {
                var foundKey = metadata.Keys.FirstOrDefault(k => 
                    k.Equals(keyVariant, StringComparison.OrdinalIgnoreCase));
                if (foundKey != null && metadata.TryGetValue(foundKey, out encryptedStr))
                {
                    encryptedKey = foundKey;
                    break;
                }
            }

            if (encryptedKey != null && encryptedStr != null)
            {
                _logger.LogDebug("Found encryption metadata: {Key}={Value}", encryptedKey, encryptedStr);
                if (bool.TryParse(encryptedStr, out var encrypted))
                {
                    isEncrypted = encrypted;
                }
                else if (encryptedStr.Equals("true", StringComparison.OrdinalIgnoreCase) || 
                         encryptedStr.Equals("1"))
                {
                    isEncrypted = true;
                }
            }
            else
            {
                _logger.LogDebug("Encryption metadata not found. Tried keys: {Keys}", 
                    string.Join(", ", encryptedKeyVariants));
            }

            // Parse compression flag
            // MinIO returns metadata keys without "x-amz-meta-" prefix
            // Try both formats: "x-amz-meta-is-zipped" and "is-zipped"
            bool isCompressed = false;
            string[] zippedKeyVariants = { "x-amz-meta-is-zipped", "is-zipped" };
            string? zippedKey = null;
            string? zippedStr = null;
            
            foreach (var keyVariant in zippedKeyVariants)
            {
                var foundKey = metadata.Keys.FirstOrDefault(k => 
                    k.Equals(keyVariant, StringComparison.OrdinalIgnoreCase));
                if (foundKey != null && metadata.TryGetValue(foundKey, out zippedStr))
                {
                    zippedKey = foundKey;
                    break;
                }
            }

            if (zippedKey != null && zippedStr != null)
            {
                _logger.LogDebug("Found compression metadata: {Key}={Value}", zippedKey, zippedStr);
                if (bool.TryParse(zippedStr, out var zipped))
                {
                    isCompressed = zipped;
                }
                else if (zippedStr.Equals("true", StringComparison.OrdinalIgnoreCase) || 
                         zippedStr.Equals("1"))
                {
                    isCompressed = true;
                }
            }
            else
            {
                _logger.LogDebug("Compression metadata not found. Tried keys: {Keys}", 
                    string.Join(", ", zippedKeyVariants));
            }

            _logger.LogDebug("Processing flags: Encrypted={IsEncrypted}, Compressed={IsCompressed}", 
                isEncrypted, isCompressed);

            // Step 3: Decrypt (if needed) - MUST be done before decompression
            if (isEncrypted)
            {
                _logger.LogDebug("Step 3: Decrypting file data ({Size} bytes)", fileData.Length);
                fileData = await _encryptionService.DecryptAsync(fileData);
                _logger.LogDebug("Decryption completed ({Size} bytes)", fileData.Length);
            }

            // Step 4: Decompress (if needed) - MUST be done after decryption
            if (isCompressed)
            {
                _logger.LogDebug("Step 4: Decompressing file data ({Size} bytes)", fileData.Length);
                fileData = await _compressionService.DecompressAsync(fileData);
                _logger.LogDebug("Decompression completed ({Size} bytes)", fileData.Length);
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

        // Detect MIME type and extension (draw.io XML has no magic bytes; honor original name)
        var (mimeType, extension) = ResolveUploadType(fileData, request.OriginalFileName);

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
    /// Builds MinIO object path (without bucket name)
    /// Format: /data/users/{datasetName}/{recordId}/{folder?}/{fileId}.{ext}
    /// Note: Bucket name (mng-{domain}) is handled separately
    /// </summary>
    private string BuildObjectPath(
        string datasetName,
        string recordId,
        string? folder,
        string fileId,
        string extension)
    {
        var pathParts = new List<string>
        {
            "data",
            "users",
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
    /// HTTP header değerleri yalnızca ASCII kabul ettiği için, MinIO metadata'ya yazılacak
    /// metinleri ASCII-safe hale getirir. Veritabanındaki file_name gerçek ad olarak kalır.
    /// </summary>
    private static string ToAsciiSafeHeaderValue(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return new string(value.Select(c => c > 127 ? '_' : c).ToArray());
    }

    /// <summary>
    /// Builds metadata dictionary for MinIO headers (all values ASCII-only for HTTP compatibility)
    /// </summary>
    private Dictionary<string, string> BuildMetadata(
        FileUploadRequestDto request,
        string domain,
        string datasetName,
        string recordId,
        string userName,
        string mimeType,
        long fileSize,
        bool isCompressed,
        bool isEncrypted)
    {
        var displayName = GetEffectiveOriginalFileName(request);
        var metadata = new Dictionary<string, string>
        {
            // File information – header için ASCII-safe; asıl ad DB'de file_name olarak saklanıyor
            ["x-amz-meta-original-filename"] = ToAsciiSafeHeaderValue(displayName),
            ["x-amz-meta-file-size"] = fileSize.ToString(),
            ["x-amz-meta-mime-type"] = mimeType,

            // Timestamps
            ["x-amz-meta-created-at"] = DateTime.UtcNow.ToString("O"),
            ["x-amz-meta-uploaded-at"] = DateTime.UtcNow.ToString("O"),

            // User & Context – kullanıcı adında Unicode olabilir
            ["x-amz-meta-uploaded-by"] = ToAsciiSafeHeaderValue(userName),
            ["x-amz-meta-domain-name"] = ToAsciiSafeHeaderValue(domain),
            ["x-amz-meta-dataset-name"] = ToAsciiSafeHeaderValue(datasetName),
            ["x-amz-meta-record-id"] = ToAsciiSafeHeaderValue(recordId),

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
    /// Magic-byte MIME plus original file name. Draw.io XML has no magic header;
    /// compressed/unknown blobs honor <c>.drawio</c> (and <c>.drawio.xml</c>) from the client name.
    /// </summary>
    private (string MimeType, string Extension) ResolveUploadType(byte[] fileData, string? originalFileName)
    {
        var mimeType = _validator.DetectMimeType(fileData);
        var extension = _validator.GetExtensionFromMimeType(mimeType);
        var namedExt = ExtensionFromOriginalName(originalFileName);

        if (string.Equals(namedExt, ".drawio", StringComparison.OrdinalIgnoreCase)
            || (string.Equals(extension, ".bin", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(namedExt)))
        {
            if (!string.IsNullOrEmpty(namedExt))
                extension = namedExt;
            if (string.Equals(namedExt, ".drawio", StringComparison.OrdinalIgnoreCase))
                mimeType = "application/vnd.jgraph.mxfile";
        }

        return (mimeType, extension);
    }

    private static string? ExtensionFromOriginalName(string? originalFileName)
    {
        if (string.IsNullOrWhiteSpace(originalFileName))
            return null;

        var name = Path.GetFileName(originalFileName.Trim());
        if (name.EndsWith(".drawio.xml", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".drawio", StringComparison.OrdinalIgnoreCase))
        {
            return ".drawio";
        }

        var ext = Path.GetExtension(name);
        return string.IsNullOrEmpty(ext) ? null : ext.ToLowerInvariant();
    }

    /// <summary>
    /// Returns the display name for the file: request.OriginalFileName if provided (path-safe),
    /// otherwise a timestamp-based fallback.
    /// </summary>
    private string GetEffectiveOriginalFileName(FileUploadRequestDto request)
    {
        var fromClient = request.OriginalFileName?.Trim();
        if (!string.IsNullOrEmpty(fromClient))
        {
            var safe = Path.GetFileName(fromClient);
            if (!string.IsNullOrEmpty(safe))
                return safe;
        }
        return GetFallbackFileName();
    }

    /// <summary>
    /// Timestamp-based fallback when client does not send original file name.
    /// </summary>
    private static string GetFallbackFileName()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        return $"file_{timestamp}";
    }
}
