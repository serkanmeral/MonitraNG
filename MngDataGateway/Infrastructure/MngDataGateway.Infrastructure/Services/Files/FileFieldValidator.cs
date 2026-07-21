using System.Text;
using System.Text.RegularExpressions;
using MngDataGateway.Application.Services.Files;
using Microsoft.Extensions.Logging;

namespace MngDataGateway.Infrastructure.Services.Files;

/// <summary>
/// File field validation service implementation
/// Validates file uploads, base64 decoding, file types, and folder paths
/// </summary>
public class FileFieldValidator : IFileFieldValidator
{
    private readonly ILogger<FileFieldValidator> _logger;
    private readonly Dictionary<string, string> _mimeToExtensionMap;
    private readonly Dictionary<string, byte[]> _magicBytesMap;

    public FileFieldValidator(ILogger<FileFieldValidator> logger)
    {
        _logger = logger;
        _mimeToExtensionMap = InitializeMimeTypeMapping();
        _magicBytesMap = InitializeMagicBytesMapping();
    }

    /// <summary>
    /// Validates and decodes base64 content
    /// </summary>
    public byte[] DecodeBase64(string base64Content)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(base64Content))
                throw new ArgumentException("Base64 content cannot be empty");

            // Remove whitespace
            var cleanBase64 = Regex.Replace(base64Content, @"\s+", "");

            // Check valid characters
            if (!Regex.IsMatch(cleanBase64, @"^[A-Za-z0-9+/]*={0,2}$"))
                throw new ArgumentException("Invalid base64 characters");

            // Decode
            var decoded = Convert.FromBase64String(cleanBase64);
            _logger.LogDebug("Base64 decoded successfully, {Size} bytes", decoded.Length);
            return decoded;
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "Base64 decode failed");
            throw new ArgumentException("Invalid base64 format", ex);
        }
    }

    /// <summary>
    /// Validates file size against maximum limit
    /// </summary>
    public bool ValidateFileSize(long fileSize, long maxSize)
    {
        var isValid = fileSize > 0 && fileSize <= maxSize;
        
        if (!isValid)
            _logger.LogWarning("File size validation failed: {FileSize} bytes (max: {MaxSize})", fileSize, maxSize);

        return isValid;
    }

    /// <summary>
    /// Validates file extension against allowed types
    /// </summary>
    public bool ValidateExtension(string extension, IEnumerable<string> allowedExtensions)
    {
        if (string.IsNullOrEmpty(extension))
            return false;

        var ext = extension.ToLowerInvariant();
        if (!ext.StartsWith("."))
            ext = "." + ext;

        var allowed = allowedExtensions.ToList();
        if (allowed.Count == 0)
            return true;  // No restrictions

        var isValid = allowed.Any(a => a.Equals(ext, StringComparison.OrdinalIgnoreCase));

        if (!isValid)
            _logger.LogWarning("Extension validation failed: {Extension} not in allowed list", ext);

        return isValid;
    }

    /// <summary>
    /// Validates folder path according to MinIO rules
    /// </summary>
    public FilePathValidationResult ValidateFolderPath(string? folderPath)
    {
        // Null or empty = default path
        if (string.IsNullOrWhiteSpace(folderPath))
            return FilePathValidationResult.Success(string.Empty);

        try
        {
            // Normalize path
            var normalized = folderPath.Trim().Replace("\\", "/");
            
            // Remove consecutive slashes
            normalized = Regex.Replace(normalized, "/+", "/");
            
            // Remove leading/trailing slashes
            normalized = normalized.Trim('/');

            // Check length
            if (normalized.Length > 512)
                return FilePathValidationResult.Failure("Path exceeds maximum length (512 chars)");

            // Check depth
            var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length > 10)
                return FilePathValidationResult.Failure("Path exceeds maximum depth (10 levels)");

            // Check forbidden sequences
            if (normalized.Contains(".."))
                return FilePathValidationResult.Failure("Relative paths (..) not allowed");

            if (normalized.Contains("~"))
                return FilePathValidationResult.Failure("Tilde (~) not allowed in path");

            // Validate each segment
            var segmentPattern = @"^[a-zA-Z0-9_\-\.]+$";
            foreach (var segment in segments)
            {
                if (!Regex.IsMatch(segment, segmentPattern))
                    return FilePathValidationResult.Failure($"Invalid characters in segment: {segment}");
            }

            _logger.LogDebug("Folder path validation passed: {Path}", normalized);
            return FilePathValidationResult.Success(normalized);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Folder path validation error");
            return FilePathValidationResult.Failure("Invalid folder path");
        }
    }

    /// <summary>
    /// Detects MIME type from file bytes using magic bytes
    /// </summary>
    public string DetectMimeType(byte[] fileBytes)
    {
        if (fileBytes == null || fileBytes.Length == 0)
            return "application/octet-stream";

        // Check magic bytes
        foreach (var (mimeType, magicBytes) in _magicBytesMap)
        {
            if (fileBytes.Length >= magicBytes.Length &&
                fileBytes.Take(magicBytes.Length).SequenceEqual(magicBytes))
            {
                _logger.LogDebug("Detected MIME type: {MimeType}", mimeType);
                return mimeType;
            }
        }

        if (LooksLikeSvg(fileBytes))
        {
            _logger.LogDebug("Detected MIME type: image/svg+xml");
            return "image/svg+xml";
        }

        _logger.LogWarning("Could not detect MIME type from magic bytes, defaulting to application/octet-stream");
        return "application/octet-stream";
    }

    private static bool LooksLikeSvg(byte[] fileBytes)
    {
        if (fileBytes == null || fileBytes.Length < 4)
            return false;

        var sampleLength = Math.Min(fileBytes.Length, 512);
        var prefix = Encoding.UTF8.GetString(fileBytes, 0, sampleLength)
            .TrimStart('\uFEFF')
            .TrimStart();

        if (!prefix.StartsWith("<svg", StringComparison.OrdinalIgnoreCase) &&
            !prefix.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
            return false;

        return prefix.Contains("<svg", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets file extension from MIME type
    /// </summary>
    public string GetExtensionFromMimeType(string mimeType)
    {
        if (string.IsNullOrEmpty(mimeType))
            return ".bin";

        var mime = mimeType.ToLowerInvariant();

        // Check exact match first
        if (_mimeToExtensionMap.TryGetValue(mime, out var ext))
            return ext;

        // Check wildcard types (e.g., "image/*")
        var baseMime = mime.Split('/')[0];
        var wildcardMime = baseMime + "/*";
        
        if (_mimeToExtensionMap.TryGetValue(wildcardMime, out var wildcardExt))
            return wildcardExt;

        _logger.LogWarning("Unknown MIME type: {MimeType}, defaulting to .bin", mimeType);
        return ".bin";
    }

    /// <summary>
    /// Validates magic bytes (file header) against detected MIME type
    /// </summary>
    public bool ValidateMagicBytes(byte[] fileBytes, string mimeType)
    {
        if (fileBytes == null || fileBytes.Length == 0)
            return false;

        var detectedMimeType = DetectMimeType(fileBytes);
        var isValid = detectedMimeType.Equals(mimeType, StringComparison.OrdinalIgnoreCase);

        if (!isValid)
            _logger.LogWarning("Magic bytes mismatch: expected {Expected}, got {Detected}", 
                mimeType, detectedMimeType);

        return isValid;
    }

    /// <summary>
    /// Initializes MIME type to extension mapping from appsettings
    /// </summary>
    private Dictionary<string, string> InitializeMimeTypeMapping()
    {
        return new(StringComparer.OrdinalIgnoreCase)
        {
            // Documents
            { "application/pdf", ".pdf" },
            { "application/msword", ".doc" },
            { "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".docx" },
            { "application/vnd.ms-excel", ".xls" },
            { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ".xlsx" },
            { "application/vnd.ms-powerpoint", ".ppt" },
            { "application/vnd.openxmlformats-officedocument.presentationml.presentation", ".pptx" },
            { "text/plain", ".txt" },
            { "text/richtext", ".rtf" },
            { "application/rtf", ".rtf" },

            // Images
            { "image/jpeg", ".jpg" },
            { "image/jpg", ".jpg" },
            { "image/png", ".png" },
            { "image/gif", ".gif" },
            { "image/webp", ".webp" },
            { "image/bmp", ".bmp" },
            { "image/svg+xml", ".svg" },
            { "image/*", ".jpg" },

            // Videos
            { "video/mp4", ".mp4" },
            { "video/avi", ".avi" },
            { "video/quicktime", ".mov" },
            { "video/x-matroska", ".mkv" },

            // Archives
            { "application/zip", ".zip" },
            { "application/x-rar-compressed", ".rar" },
            { "application/x-7z-compressed", ".7z" },

            // Default
            { "application/octet-stream", ".bin" }
        };
    }

    /// <summary>
    /// Initializes magic bytes mapping for common file types
    /// </summary>
    private Dictionary<string, byte[]> InitializeMagicBytesMapping()
    {
        return new()
        {
            // PDF: %PDF
            { "application/pdf", new byte[] { 0x25, 0x50, 0x44, 0x46 } },

            // JPEG: FF D8 FF
            { "image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF } },

            // PNG: 89 50 4E 47
            { "image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47 } },

            // GIF: GIF8
            { "image/gif", new byte[] { 0x47, 0x49, 0x46, 0x38 } },

            // ZIP: PK (50 4B)
            { "application/zip", new byte[] { 0x50, 0x4B } },

            // DOCX/XLSX/PPTX (ZIP format): PK
            { "application/vnd.openxmlformats-officedocument.wordprocessingml.document", new byte[] { 0x50, 0x4B } },
            { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", new byte[] { 0x50, 0x4B } },
            { "application/vnd.openxmlformats-officedocument.presentationml.presentation", new byte[] { 0x50, 0x4B } },

            // MP4: ftyp at offset 4
            { "video/mp4", new byte[] { 0x66, 0x74, 0x79, 0x70 } },

            // WebP: RIFF...WEBP
            { "image/webp", new byte[] { 0x52, 0x49, 0x46, 0x46 } },

            // GZIP: 1F 8B
            { "application/gzip", new byte[] { 0x1F, 0x8B } }
        };
    }
}
