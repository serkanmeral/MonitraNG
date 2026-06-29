using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngDataGateway.Api.Helpers;
using MngDataGateway.Application.Configuration;
using MngDataGateway.Application.DTOs.Common;
using MngDataGateway.Application.DTOs.Files;
using MngDataGateway.Application.Services;
using MngDataGateway.Application.Services.Files;
using MngDataGateway.Domain.Entities;
using MngDataGateway.Domain.Exceptions;
using Microsoft.Extensions.Options;

namespace MngDataGateway.Api.Controllers;

/// <summary>
/// File upload and download operations controller
/// Handles file storage, compression, encryption, and retrieval
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/files")]
[Authorize]
[Produces("application/json")]
public class FilesController : ControllerBase
{
    private readonly ILogger<FilesController> _logger;
    private readonly IFileProcessingPipeline _fileProcessingPipeline;
    private readonly IMinIOFileService _minioService;
    private readonly IMongoContextService _mongoContextService;
    private readonly IUserInfoService _userInfoService;
    private readonly IDatasetService _datasetService;
    private readonly IPermissionService _permissionService;
    private readonly MngDataGatewaySettings _settings;

    public FilesController(
        ILogger<FilesController> logger,
        IFileProcessingPipeline fileProcessingPipeline,
        IMinIOFileService minioService,
        IMongoContextService mongoContextService,
        IUserInfoService userInfoService,
        IDatasetService datasetService,
        IPermissionService permissionService,
        IOptions<MngDataGatewaySettings> options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fileProcessingPipeline = fileProcessingPipeline ?? throw new ArgumentNullException(nameof(fileProcessingPipeline));
        _minioService = minioService ?? throw new ArgumentNullException(nameof(minioService));
        _mongoContextService = mongoContextService ?? throw new ArgumentNullException(nameof(mongoContextService));
        _userInfoService = userInfoService ?? throw new ArgumentNullException(nameof(userInfoService));
        _datasetService = datasetService ?? throw new ArgumentNullException(nameof(datasetService));
        _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
        _settings = options.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Upload file to MinIO storage
    /// Processes file: validates, compresses (optional), encrypts (optional), uploads
    /// </summary>
    /// <param name="request">File upload request with dataset context</param>
    /// <returns>File upload result with path and metadata</returns>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(DataResponseDto<FileUploadResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Upload([FromBody] FileUploadRequestDto request)
    {
        var uploadPath = "/api/v1/files/upload";
        
        try
        {
            // 1. Extract domain from JWT token
            var domainName = _mongoContextService.GetCurrentDomainName();
            if (string.IsNullOrEmpty(domainName))
            {
                return this.ErrorResponse(uploadPath, "FORBIDDEN", "Domain information not found in token", statusCode: 403);
            }

            // 2. Validate request
            if (request == null)
            {
                return this.ErrorResponse(uploadPath, "INVALID_REQUEST", "Request body cannot be null");
            }

            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return this.ErrorResponse(uploadPath, "INVALID_REQUEST", "File content (base64) is required");
            }

            // 3. Get dataset schema for validation and permission check
            var datasetName = request.DatasetName ?? throw new ArgumentException("Dataset name is required");
            var schema = await _datasetService.GetSchemaEntityByNameAsync(datasetName);
            if (schema == null)
            {
                return this.ErrorResponse(uploadPath, "DATASET_NOT_FOUND", $"Dataset '{datasetName}' not found", statusCode: 404);
            }

            // 4. Check if field is file type
            var fieldName = request.FieldName ?? throw new ArgumentException("Field name is required");
            var field = schema.fields?.FirstOrDefault(f => f.name == fieldName);
            if (field == null)
            {
                return this.ErrorResponse(uploadPath, "FIELD_NOT_FOUND", $"Field '{fieldName}' not found in dataset '{datasetName}'");
            }

            if (field.fieldType != "file")
            {
                return this.ErrorResponse(uploadPath, "INVALID_FIELD_TYPE", $"Field '{fieldName}' is not a file field type");
            }

            // 5. Check permission
            var userGroups = _permissionService.GetUserGroups(HttpContext);
            var hasPermission = _permissionService.CheckPermission(schema, "create", userGroups, domainName);
            if (!hasPermission)
            {
                return this.ErrorResponse(uploadPath, "FORBIDDEN", $"You don't have 'create' permission for dataset '{datasetName}'", statusCode: 403);
            }

            // 6. Get file options from field definition or use defaults
            var fileOptions = GetFileOptionsFromField(field, _settings.FileStorage.Validation);

            // 7. Generate record ID if not provided (for new records)
            var recordId = request.RecordId ?? Guid.NewGuid().ToString();

            // 8. Process file upload
            var userInfo = _userInfoService.GetCurrentUserInfo();
            var processingResult = await _fileProcessingPipeline.ProcessFileUploadAsync(
                new FileUploadRequestDto
                {
                    Content = request.Content,
                    Folder = request.Folder,
                    UseCompression = request.UseCompression,
                    UseEncryption = request.UseEncryption
                },
                domainName,
                datasetName,
                recordId,
                userInfo.userName,
                fileOptions,
                HttpContext.RequestAborted);

            // 9. Build response
            var response = new FileUploadResponseDto
            {
                FilePath = processingResult.FilePath,
                OriginalFileName = processingResult.OriginalFileName,
                FileSize = processingResult.OriginalFileSize,
                MimeType = processingResult.MimeType,
                IsCompressed = processingResult.IsCompressed,
                IsEncrypted = processingResult.IsEncrypted,
                UploadedAt = processingResult.UploadedAt
            };

            _logger.LogInformation(
                "File uploaded successfully: {FilePath} ({Size} bytes, Compressed: {Compressed}, Encrypted: {Encrypted})",
                processingResult.FilePath,
                processingResult.OriginalFileSize,
                processingResult.IsCompressed,
                processingResult.IsEncrypted);

            return this.SuccessResponse(response, uploadPath);
        }
        catch (ArgumentException ex)
        {
            return this.ErrorResponse(uploadPath, "INVALID_REQUEST", ex.Message);
        }
        catch (Exception ex)
        {
            return this.HandleException(ex, uploadPath, "UPLOAD_FAILED", "Failed to upload file", _logger, includeStackTrace: true);
        }
    }

    /// <summary>
    /// Download file from MinIO storage
    /// Retrieves file, decrypts (if encrypted), decompresses (if compressed)
    /// </summary>
    /// <param name="filePath">File path in MinIO (e.g., /mng-domain/data/dataset/record/file.pdf)</param>
    /// <returns>File content as binary stream</returns>
    [HttpGet("download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Download([FromQuery] string filePath)
    {
        var downloadPath = "/api/v1/files/download";
        
        try
        {
            // 1. Validate file path
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return this.ErrorResponse(downloadPath, "INVALID_REQUEST", "File path is required");
            }

            // 2. Extract domain from path
            var pathParts = filePath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (pathParts.Length < 2 || !pathParts[0].StartsWith("mng-"))
            {
                return this.ErrorResponse(downloadPath, "INVALID_PATH", "Invalid file path format");
            }

            var domainName = pathParts[0].Replace("mng-", "");
            var currentDomain = _mongoContextService.GetCurrentDomainName();

            // 3. Check domain access (users can only access files from their domain)
            if (domainName != currentDomain)
            {
                return this.ErrorResponse(downloadPath, "FORBIDDEN", "Access denied: File belongs to different domain", statusCode: 403);
            }

            // 4. Extract dataset name from path
            if (pathParts.Length < 5 || pathParts[1] != "data" || pathParts[2] != "users")
            {
                return this.ErrorResponse(downloadPath, "INVALID_PATH", "Invalid file path format: missing dataset/record info");
            }

            var datasetName = pathParts[3];

            // 5. Get dataset schema for permission check
            var schema = await _datasetService.GetSchemaEntityByNameAsync(datasetName);
            if (schema == null)
            {
                return this.ErrorResponse(downloadPath, "DATASET_NOT_FOUND", $"Dataset '{datasetName}' not found", statusCode: 404);
            }

            // 6. Check permission
            var userGroups = _permissionService.GetUserGroups(HttpContext);
            var hasPermission = _permissionService.CheckPermission(schema, "read", userGroups, domainName);
            if (!hasPermission)
            {
                return this.ErrorResponse(downloadPath, "FORBIDDEN", $"You don't have 'read' permission for dataset '{datasetName}'", statusCode: 403);
            }

            // 7. Process file download (decrypt, decompress)
            var fileContent = await _fileProcessingPipeline.ProcessFileDownloadAsync(
                domainName,
                filePath,
                HttpContext.RequestAborted);

            // 8. Get metadata for content type and filename
            var bucketName = $"mng-{domainName}";
            // Extract MinIO object path (remove bucket name prefix)
            string objectPath = filePath;
            if (filePath.StartsWith($"/mng-{domainName}/"))
            {
                objectPath = filePath.Substring($"/mng-{domainName}".Length);
            }
            var metadata = await _minioService.GetFileMetadataAsync(bucketName, objectPath, HttpContext.RequestAborted);
            
            var contentType = metadata.TryGetValue("x-amz-meta-mime-type", out var mimeType) 
                ? mimeType 
                : "application/octet-stream";
            
            var originalFileName = metadata.TryGetValue("x-amz-meta-original-filename", out var fileName)
                ? fileName
                : Path.GetFileName(filePath);

            _logger.LogInformation(
                "File downloaded successfully: {FilePath} ({Size} bytes)",
                filePath,
                fileContent.Length);

            // 9. Return file as binary stream
            return File(fileContent, contentType, originalFileName);
        }
        catch (FileNotFoundException ex)
        {
            return this.ErrorResponse(downloadPath, "FILE_NOT_FOUND", ex.Message, statusCode: 404);
        }
        catch (UnauthorizedAccessException ex)
        {
            return this.ErrorResponse(downloadPath, "FORBIDDEN", ex.Message, statusCode: 403);
        }
        catch (Exception ex)
        {
            return this.HandleError(ex, downloadPath, "DOWNLOAD_FAILED", "Failed to download file", _logger, includeStackTrace: true);
        }
    }

    /// <summary>
    /// Get file metadata without downloading content
    /// </summary>
    /// <param name="filePath">File path in MinIO (e.g., /mng-domain/data/users/dataset/record/file.pdf)</param>
    /// <returns>File metadata including size, MIME type, compression/encryption flags, timestamps, etc.</returns>
    [HttpGet("metadata")]
    [ProducesResponseType(typeof(DataResponseDto<FileMetadataResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetMetadata([FromQuery] string filePath)
    {
        var metadataPath = "/api/v1/files/metadata";
        
        try
        {
            // 1. Validate file path
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return this.ErrorResponse(metadataPath, "INVALID_REQUEST", "File path is required");
            }

            // 2. Extract domain from path
            var pathParts = filePath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (pathParts.Length < 2 || !pathParts[0].StartsWith("mng-"))
            {
                return this.ErrorResponse(metadataPath, "INVALID_PATH", "Invalid file path format");
            }

            var domainName = pathParts[0].Replace("mng-", "");
            var currentDomain = _mongoContextService.GetCurrentDomainName();

            // 3. Check domain access
            if (domainName != currentDomain)
            {
                return this.ErrorResponse(metadataPath, "FORBIDDEN", "Access denied: File belongs to different domain", statusCode: 403);
            }

            // 4. Extract dataset name from path
            if (pathParts.Length < 5 || pathParts[1] != "data" || pathParts[2] != "users")
            {
                return this.ErrorResponse(metadataPath, "INVALID_PATH", "Invalid file path format: missing dataset/record info");
            }

            var datasetName = pathParts[3];

            // 5. Get dataset schema for permission check
            var schema = await _datasetService.GetSchemaEntityByNameAsync(datasetName);
            if (schema == null)
            {
                return this.ErrorResponse(metadataPath, "DATASET_NOT_FOUND", $"Dataset '{datasetName}' not found", statusCode: 404);
            }

            // 6. Check permission
            var userGroups = _permissionService.GetUserGroups(HttpContext);
            var hasPermission = _permissionService.CheckPermission(schema, "read", userGroups, domainName);
            if (!hasPermission)
            {
                return this.ErrorResponse(metadataPath, "FORBIDDEN", $"You don't have 'read' permission for dataset '{datasetName}'", statusCode: 403);
            }

            // 7. Get metadata from MinIO
            var bucketName = $"mng-{domainName}";
            // Extract MinIO object path (remove bucket name prefix)
            string objectPath = filePath;
            if (filePath.StartsWith($"/mng-{domainName}/"))
            {
                objectPath = filePath.Substring($"/mng-{domainName}".Length);
            }
            var metadata = await _minioService.GetFileMetadataAsync(bucketName, objectPath, HttpContext.RequestAborted);

            // 8. Build structured response
            var response = new FileMetadataResponseDto
            {
                FilePath = filePath,
                OriginalFileName = metadata.TryGetValue("original-filename", out var fileName) 
                    ? fileName 
                    : metadata.TryGetValue("x-amz-meta-original-filename", out var fileName2) 
                        ? fileName2 
                        : Path.GetFileName(filePath),
                FileSize = metadata.TryGetValue("file-size", out var sizeStr) && long.TryParse(sizeStr, out var size)
                    ? size
                    : metadata.TryGetValue("x-amz-meta-file-size", out var sizeStr2) && long.TryParse(sizeStr2, out var size2)
                        ? size2
                        : metadata.TryGetValue("content-length", out var sizeStr3) && long.TryParse(sizeStr3, out var size3)
                            ? size3
                            : 0,
                MimeType = metadata.TryGetValue("mime-type", out var mimeType)
                    ? mimeType
                    : metadata.TryGetValue("x-amz-meta-mime-type", out var mimeType2)
                        ? mimeType2
                        : metadata.TryGetValue("content-type", out var contentType)
                            ? contentType
                            : "application/octet-stream",
                IsCompressed = metadata.TryGetValue("is-zipped", out var zippedStr) && bool.TryParse(zippedStr, out var zipped)
                    ? zipped
                    : metadata.TryGetValue("x-amz-meta-is-zipped", out var zippedStr2) && bool.TryParse(zippedStr2, out var zipped2)
                        ? zipped2
                        : false,
                IsEncrypted = metadata.TryGetValue("is-encrypted", out var encryptedStr) && bool.TryParse(encryptedStr, out var encrypted)
                    ? encrypted
                    : metadata.TryGetValue("x-amz-meta-is-encrypted", out var encryptedStr2) && bool.TryParse(encryptedStr2, out var encrypted2)
                        ? encrypted2
                        : false,
                UploadedBy = metadata.TryGetValue("uploaded-by", out var uploadedBy)
                    ? uploadedBy
                    : metadata.TryGetValue("x-amz-meta-uploaded-by", out var uploadedBy2)
                        ? uploadedBy2
                        : string.Empty,
                DatasetName = metadata.TryGetValue("dataset-name", out var dataset)
                    ? dataset
                    : metadata.TryGetValue("x-amz-meta-dataset-name", out var dataset2)
                        ? dataset2
                        : datasetName,
                RecordId = metadata.TryGetValue("record-id", out var recordId)
                    ? recordId
                    : metadata.TryGetValue("x-amz-meta-record-id", out var recordId2)
                        ? recordId2
                        : string.Empty,
                CreatedAt = metadata.TryGetValue("created-at", out var createdAtStr) && DateTime.TryParse(createdAtStr, out var createdAt)
                    ? createdAt
                    : metadata.TryGetValue("x-amz-meta-created-at", out var createdAtStr2) && DateTime.TryParse(createdAtStr2, out var createdAt2)
                        ? createdAt2
                        : DateTime.MinValue,
                UploadedAt = metadata.TryGetValue("uploaded-at", out var uploadedAtStr) && DateTime.TryParse(uploadedAtStr, out var uploadedAt)
                    ? uploadedAt
                    : metadata.TryGetValue("x-amz-meta-uploaded-at", out var uploadedAtStr2) && DateTime.TryParse(uploadedAtStr2, out var uploadedAt2)
                        ? uploadedAt2
                        : metadata.TryGetValue("last-modified", out var lastModifiedStr) && DateTime.TryParse(lastModifiedStr, out var lastModified)
                            ? lastModified
                            : DateTime.MinValue,
                RawMetadata = metadata  // Include raw metadata for advanced use cases
            };

            _logger.LogInformation(
                "File metadata retrieved successfully: {FilePath} ({Size} bytes, Compressed: {Compressed}, Encrypted: {Encrypted})",
                filePath, response.FileSize, response.IsCompressed, response.IsEncrypted);

            return this.SuccessResponse(response, metadataPath);
        }
        catch (FileNotFoundException ex)
        {
            return this.ErrorResponse(metadataPath, "FILE_NOT_FOUND", ex.Message, statusCode: 404);
        }
        catch (Exception ex)
        {
            return this.HandleError(ex, metadataPath, "GET_METADATA_FAILED", "Failed to get file metadata", _logger, includeStackTrace: true);
        }
    }

    #region Helper Methods

    /// <summary>
    /// Gets file processing options from field definition or uses defaults
    /// </summary>
    private FileProcessingOptionsDto GetFileOptionsFromField(
        MngDataGateway.Domain.Entities.FieldDefinition field,
        ValidationSettings validationSettings)
    {
        // For now, use configuration defaults
        // TODO: Phase 2+ - Parse fileOptions from field definition if available
        // Example: field.fileOptions?.maxSize, field.fileOptions?.allowedExtensions, etc.
        
        var options = new FileProcessingOptionsDto
        {
            MaxFileSize = validationSettings.MaxFileSize,
            AllowedExtensions = validationSettings.AllowedExtensions ?? new List<string>(),
            MaxFolderDepth = validationSettings.MaxFolderDepth,
            MaxPathLength = validationSettings.MaxPathLength,
            DefaultCompression = _settings.FileStorage.Compression.Enabled,
            DefaultEncryption = _settings.FileStorage.Encryption.Enabled,
            CompressionLevel = _settings.FileStorage.Compression.Level
        };

        // Future: Parse from field.fileOptions when implemented
        // if (field.fileOptions != null)
        // {
        //     options.MaxFileSize = field.fileOptions.maxSize ?? options.MaxFileSize;
        //     options.AllowedExtensions = field.fileOptions.allowedExtensions ?? options.AllowedExtensions;
        //     // etc.
        // }
        
        return options;
    }

    #endregion
}
