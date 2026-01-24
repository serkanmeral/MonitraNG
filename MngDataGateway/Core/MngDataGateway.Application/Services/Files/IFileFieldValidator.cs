namespace MngDataGateway.Application.Services.Files;

/// <summary>
/// File field validation service interface
/// Handles validation for file uploads, base64 decoding, file type checking, etc.
/// </summary>
public interface IFileFieldValidator
{
    /// <summary>
    /// Validates and decodes base64 content
    /// </summary>
    /// <param name="base64Content">Base64 encoded file content</param>
    /// <returns>Decoded binary data</returns>
    /// <exception cref="ArgumentException">If base64 is invalid</exception>
    byte[] DecodeBase64(string base64Content);

    /// <summary>
    /// Validates file size against maximum limit
    /// </summary>
    /// <param name="fileSize">File size in bytes</param>
    /// <param name="maxSize">Maximum allowed size in bytes</param>
    /// <returns>True if valid, false otherwise</returns>
    bool ValidateFileSize(long fileSize, long maxSize);

    /// <summary>
    /// Validates file extension against allowed types
    /// </summary>
    /// <param name="extension">File extension (e.g., ".pdf")</param>
    /// <param name="allowedExtensions">List of allowed extensions</param>
    /// <returns>True if valid, false otherwise</returns>
    bool ValidateExtension(string extension, IEnumerable<string> allowedExtensions);

    /// <summary>
    /// Validates folder path according to MinIO rules
    /// </summary>
    /// <param name="folderPath">Folder path to validate</param>
    /// <returns>Validation result with error message if invalid</returns>
    FilePathValidationResult ValidateFolderPath(string? folderPath);

    /// <summary>
    /// Detects MIME type from file bytes using magic bytes
    /// </summary>
    /// <param name="fileBytes">File content bytes</param>
    /// <returns>Detected MIME type</returns>
    string DetectMimeType(byte[] fileBytes);

    /// <summary>
    /// Gets file extension from MIME type using configuration mapping
    /// </summary>
    /// <param name="mimeType">MIME type (e.g., "application/pdf")</param>
    /// <returns>File extension (e.g., ".pdf")</returns>
    string GetExtensionFromMimeType(string mimeType);

    /// <summary>
    /// Validates magic bytes (file header) against detected MIME type
    /// </summary>
    /// <param name="fileBytes">File content bytes</param>
    /// <param name="mimeType">Expected MIME type</param>
    /// <returns>True if magic bytes match, false otherwise</returns>
    bool ValidateMagicBytes(byte[] fileBytes, string mimeType);
}

/// <summary>
/// File path validation result
/// </summary>
public class FilePathValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public string? NormalizedPath { get; set; }

    public static FilePathValidationResult Success(string normalizedPath)
        => new() { IsValid = true, NormalizedPath = normalizedPath };

    public static FilePathValidationResult Failure(string errorMessage)
        => new() { IsValid = false, ErrorMessage = errorMessage };
}
