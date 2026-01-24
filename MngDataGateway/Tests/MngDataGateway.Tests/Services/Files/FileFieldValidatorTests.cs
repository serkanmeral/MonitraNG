using Xunit;
using MngDataGateway.Infrastructure.Services.Files;
using MngDataGateway.Tests.Helpers;

namespace MngDataGateway.Tests.Services.Files;

/// <summary>
/// Unit tests for FileFieldValidator
/// </summary>
public class FileFieldValidatorTests
{
    private readonly FileFieldValidator _validator;

    public FileFieldValidatorTests()
    {
        var logger = LoggerMockHelper.CreateMockLogger<FileFieldValidator>();
        _validator = new FileFieldValidator(logger);
    }

    #region Base64 Decoding Tests

    [Fact]
    public void DecodeBase64_ValidInput_Success()
    {
        // Arrange
        var testString = "Hello, World!";
        var base64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(testString));

        // Act
        var result = _validator.DecodeBase64(base64);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(testString, System.Text.Encoding.UTF8.GetString(result));
    }

    [Fact]
    public void DecodeBase64_EmptyString_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _validator.DecodeBase64(""));
    }

    [Fact]
    public void DecodeBase64_InvalidCharacters_Throws()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _validator.DecodeBase64("!!!invalid!!!"));
    }

    [Fact]
    public void DecodeBase64_WithWhitespace_Success()
    {
        // Arrange
        var base64 = "SGVs bG8s IFdv cmxk";  // "Hello, World" with spaces

        // Act
        var result = _validator.DecodeBase64(base64);

        // Assert
        Assert.NotNull(result);
    }

    #endregion

    #region File Size Validation Tests

    [Fact]
    public void ValidateFileSize_WithinLimit_Success()
    {
        // Arrange
        long fileSize = 50 * 1024 * 1024;  // 50MB
        long maxSize = 100 * 1024 * 1024;  // 100MB

        // Act
        var result = _validator.ValidateFileSize(fileSize, maxSize);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ValidateFileSize_AtLimit_Success()
    {
        // Arrange
        long fileSize = 100 * 1024 * 1024;  // 100MB
        long maxSize = 100 * 1024 * 1024;   // 100MB

        // Act
        var result = _validator.ValidateFileSize(fileSize, maxSize);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ValidateFileSize_ExceedsLimit_Failure()
    {
        // Arrange
        long fileSize = 150 * 1024 * 1024;  // 150MB
        long maxSize = 100 * 1024 * 1024;   // 100MB

        // Act
        var result = _validator.ValidateFileSize(fileSize, maxSize);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateFileSize_Zero_Failure()
    {
        // Act
        var result = _validator.ValidateFileSize(0, 100);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Extension Validation Tests

    [Fact]
    public void ValidateExtension_AllowedExtension_Success()
    {
        // Arrange
        var extension = ".pdf";
        var allowed = new[] { ".pdf", ".docx", ".xlsx" };

        // Act
        var result = _validator.ValidateExtension(extension, allowed);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ValidateExtension_CaseInsensitive_Success()
    {
        // Arrange
        var extension = ".PDF";
        var allowed = new[] { ".pdf", ".docx" };

        // Act
        var result = _validator.ValidateExtension(extension, allowed);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ValidateExtension_NotAllowed_Failure()
    {
        // Arrange
        var extension = ".exe";
        var allowed = new[] { ".pdf", ".docx", ".xlsx" };

        // Act
        var result = _validator.ValidateExtension(extension, allowed);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateExtension_NoRestrictions_Success()
    {
        // Arrange
        var extension = ".anything";
        var allowed = Array.Empty<string>();

        // Act
        var result = _validator.ValidateExtension(extension, allowed);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ValidateExtension_Empty_Failure()
    {
        // Act
        var result = _validator.ValidateExtension("", new[] { ".pdf" });

        // Assert
        Assert.False(result);
    }

    #endregion

    #region MIME Type Detection Tests

    [Fact]
    public void DetectMimeType_PDF_Success()
    {
        // Arrange
        byte[] pdfBytes = { 0x25, 0x50, 0x44, 0x46 };  // %PDF

        // Act
        var mimeType = _validator.DetectMimeType(pdfBytes);

        // Assert
        Assert.Equal("application/pdf", mimeType);
    }

    [Fact]
    public void DetectMimeType_PNG_Success()
    {
        // Arrange
        byte[] pngBytes = { 0x89, 0x50, 0x4E, 0x47 };  // PNG signature

        // Act
        var mimeType = _validator.DetectMimeType(pngBytes);

        // Assert
        Assert.Equal("image/png", mimeType);
    }

    [Fact]
    public void DetectMimeType_JPEG_Success()
    {
        // Arrange
        byte[] jpegBytes = { 0xFF, 0xD8, 0xFF };

        // Act
        var mimeType = _validator.DetectMimeType(jpegBytes);

        // Assert
        Assert.Equal("image/jpeg", mimeType);
    }

    [Fact]
    public void DetectMimeType_Unknown_DefaultsToOctetStream()
    {
        // Arrange
        byte[] unknownBytes = { 0x00, 0x01, 0x02 };

        // Act
        var mimeType = _validator.DetectMimeType(unknownBytes);

        // Assert
        Assert.Equal("application/octet-stream", mimeType);
    }

    [Fact]
    public void DetectMimeType_EmptyArray_DefaultsToOctetStream()
    {
        // Act
        var mimeType = _validator.DetectMimeType(Array.Empty<byte>());

        // Assert
        Assert.Equal("application/octet-stream", mimeType);
    }

    #endregion

    #region Extension from MIME Type Tests

    [Fact]
    public void GetExtensionFromMimeType_PDF_Success()
    {
        // Act
        var extension = _validator.GetExtensionFromMimeType("application/pdf");

        // Assert
        Assert.Equal(".pdf", extension);
    }

    [Fact]
    public void GetExtensionFromMimeType_JPEG_Success()
    {
        // Act
        var extension = _validator.GetExtensionFromMimeType("image/jpeg");

        // Assert
        Assert.Equal(".jpg", extension);
    }

    [Fact]
    public void GetExtensionFromMimeType_CaseInsensitive_Success()
    {
        // Act
        var extension = _validator.GetExtensionFromMimeType("APPLICATION/PDF");

        // Assert
        Assert.Equal(".pdf", extension);
    }

    [Fact]
    public void GetExtensionFromMimeType_Unknown_DefaultsToBin()
    {
        // Act
        var extension = _validator.GetExtensionFromMimeType("application/unknown");

        // Assert
        Assert.Equal(".bin", extension);
    }

    [Fact]
    public void GetExtensionFromMimeType_Empty_DefaultsToBin()
    {
        // Act
        var extension = _validator.GetExtensionFromMimeType("");

        // Assert
        Assert.Equal(".bin", extension);
    }

    #endregion

    #region Folder Path Validation Tests

    [Fact]
    public void ValidateFolderPath_ValidPath_Success()
    {
        // Act
        var result = _validator.ValidateFolderPath("documents/2025");

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("documents/2025", result.NormalizedPath);
    }

    [Fact]
    public void ValidateFolderPath_Empty_Success()
    {
        // Act
        var result = _validator.ValidateFolderPath("");

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("", result.NormalizedPath);
    }

    [Fact]
    public void ValidateFolderPath_Null_Success()
    {
        // Act
        var result = _validator.ValidateFolderPath(null);

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateFolderPath_WithTraversal_Failure()
    {
        // Act
        var result = _validator.ValidateFolderPath("../../../etc/passwd");

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Relative paths", result.ErrorMessage);
    }

    [Fact]
    public void ValidateFolderPath_TooDeep_Failure()
    {
        // Act
        var result = _validator.ValidateFolderPath("a/b/c/d/e/f/g/h/i/j/k");

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("depth", result.ErrorMessage?.ToLowerInvariant() ?? "");
    }

    [Fact]
    public void ValidateFolderPath_TooLong_Failure()
    {
        // Arrange
        var longPath = string.Join("/", Enumerable.Repeat("verylongfoldername", 50));

        // Act
        var result = _validator.ValidateFolderPath(longPath);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("length", result.ErrorMessage?.ToLowerInvariant() ?? "");
    }

    [Fact]
    public void ValidateFolderPath_SpecialCharacters_Failure()
    {
        // Act
        var result = _validator.ValidateFolderPath("documents/2025&*@");

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateFolderPath_NormalizesSlashes_Success()
    {
        // Act
        var result = _validator.ValidateFolderPath("documents//2025///test");

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("documents/2025/test", result.NormalizedPath);
    }

    #endregion

    #region Magic Bytes Validation Tests

    [Fact]
    public void ValidateMagicBytes_PDFCorrectType_Success()
    {
        // Arrange
        byte[] pdfBytes = { 0x25, 0x50, 0x44, 0x46 };  // %PDF

        // Act
        var result = _validator.ValidateMagicBytes(pdfBytes, "application/pdf");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ValidateMagicBytes_MismatchedType_Failure()
    {
        // Arrange
        byte[] pdfBytes = { 0x25, 0x50, 0x44, 0x46 };  // %PDF

        // Act
        var result = _validator.ValidateMagicBytes(pdfBytes, "image/png");

        // Assert
        Assert.False(result);
    }

    #endregion
}
