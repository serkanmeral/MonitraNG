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
        catch (DataGatewayException ex) when (ex.ValidationErrors != null)
        {
            return this.HandleValidationError(ex, uploadPath, _logger);
        }
        catch (Exception ex)
        {
            return this.HandleError(ex, uploadPath, "UPLOAD_FAILED", "Failed to upload file", _logger, includeStackTrace: true);
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
            if (pathParts.Length < 4 || pathParts[1] != "data")
            {
                return this.ErrorResponse(downloadPath, "INVALID_PATH", "Invalid file path format: missing dataset/record info");
            }

            var datasetName = pathParts[2];

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
            var metadata = await _minioService.GetFileMetadataAsync(bucketName, filePath, HttpContext.RequestAborted);
            
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
    /// <param name="filePath">File path in MinIO</param>
    /// <returns>File metadata</returns>
    [HttpGet("metadata")]
    [ProducesResponseType(typeof(DataResponseDto<Dictionary<string, string>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMetadata([FromQuery] string filePath)
    {
        var metadataPath = "/api/v1/files/metadata";
        
        try
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return this.ErrorResponse(metadataPath, "INVALID_REQUEST", "File path is required");
            }

            // Extract domain from path
            var pathParts = filePath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (pathParts.Length < 2 || !pathParts[0].StartsWith("mng-"))
            {
                return this.ErrorResponse(metadataPath, "INVALID_PATH", "Invalid file path format");
            }

            var domainName = pathParts[0].Replace("mng-", "");
            var currentDomain = _mongoContextService.GetCurrentDomainName();

            // Check domain access
            if (domainName != currentDomain)
            {
                return this.ErrorResponse(metadataPath, "FORBIDDEN", "Access denied: File belongs to different domain", statusCode: 403);
            }

            var bucketName = $"mng-{domainName}";
            var metadata = await _minioService.GetFileMetadataAsync(bucketName, filePath, HttpContext.RequestAborted);

            return this.SuccessResponse(metadata, metadataPath);
        }
        catch (FileNotFoundException ex)
        {
            return this.ErrorResponse(metadataPath, "FILE_NOT_FOUND", ex.Message, statusCode: 404);
        }
        catch (Exception ex)
        {
            return this.HandleError(ex, metadataPath, "GET_METADATA_FAILED", "Failed to get file metadata", _logger);
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
