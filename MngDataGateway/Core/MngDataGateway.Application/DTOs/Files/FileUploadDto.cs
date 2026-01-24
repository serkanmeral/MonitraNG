namespace MngDataGateway.Application.DTOs.Files;

/// <summary>
/// File upload request DTO
/// Represents a single file upload in POST/PUT request body
/// </summary>
public class FileUploadRequestDto
{
    /// <summary>
    /// Base64 encoded file content (REQUIRED)
    /// </summary>
    public required string Content { get; set; }

    /// <summary>
    /// Dataset name (REQUIRED for API requests)
    /// Example: "@invoices"
    /// </summary>
    public string? DatasetName { get; set; }

    /// <summary>
    /// Field name in dataset (REQUIRED for API requests)
    /// Example: "documentFile"
    /// </summary>
    public string? FieldName { get; set; }

    /// <summary>
    /// Record ID (OPTIONAL)
    /// If null, generates new GUID (for new records)
    /// Example: "TASK-000001"
    /// </summary>
    public string? RecordId { get; set; }

    /// <summary>
    /// Custom folder path (OPTIONAL)
    /// If null/empty, uses default: /mng-{domain}/data/{dataset}/{record}/
    /// Example: "invoices/2025" → /mng-{domain}/data/{dataset}/{record}/invoices/2025/
    /// </summary>
    public string? Folder { get; set; }

    /// <summary>
    /// Enable gzip compression (OPTIONAL, default: true from config)
    /// If compression fails, continues without compression (non-fatal)
    /// </summary>
    public bool? UseCompression { get; set; }

    /// <summary>
    /// Enable AES-256-GCM encryption (OPTIONAL, default: true from config)
    /// If encryption fails, throws exception (fatal)
    /// </summary>
    public bool? UseEncryption { get; set; }
}

/// <summary>
/// File upload response DTO
/// Represents file metadata in response
/// </summary>
public class FileUploadResponseDto
{
    /// <summary>
    /// File path in MinIO
    /// Example: /mng-meral/data/@invoices/TASK-000001/invoices/2025/a7f3k9m2.pdf
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Original filename
    /// </summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// MIME type
    /// </summary>
    public string MimeType { get; set; } = string.Empty;

    /// <summary>
    /// Whether file was compressed
    /// </summary>
    public bool IsCompressed { get; set; }

    /// <summary>
    /// Whether file was encrypted
    /// </summary>
    public bool IsEncrypted { get; set; }

    /// <summary>
    /// Upload timestamp
    /// </summary>
    public DateTime UploadedAt { get; set; }
}

/// <summary>
/// File processing options DTO
/// Used internally during file processing
/// </summary>
public class FileProcessingOptionsDto
{
    public long MaxFileSize { get; set; } = 104857600;  // 100MB
    public List<string> AllowedExtensions { get; set; } = new();
    public int MaxFolderDepth { get; set; } = 10;
    public int MaxPathLength { get; set; } = 512;
    public bool DefaultCompression { get; set; } = true;
    public bool DefaultEncryption { get; set; } = true;
    public int CompressionLevel { get; set; } = 6;  // 1-9
}

/// <summary>
/// File metadata DTO
/// Contains metadata to be stored in MinIO headers
/// </summary>
public class FileMetadataDto
{
    public string OriginalFileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string UploadedBy { get; set; } = string.Empty;
    public string DomainName { get; set; } = string.Empty;
    public string DatasetName { get; set; } = string.Empty;
    public string RecordId { get; set; } = string.Empty;
    public bool IsZipped { get; set; }
    public bool IsEncrypted { get; set; }
    public EncryptionConfigDto? EncryptionConfig { get; set; }
}

/// <summary>
/// Encryption configuration DTO for metadata
/// </summary>
public class EncryptionConfigDto
{
    public string Algorithm { get; set; } = "AES-256-GCM";
    public string KeyDerivation { get; set; } = "PBKDF2";
}
