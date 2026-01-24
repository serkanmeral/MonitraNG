using Xunit;
using MngDataGateway.Infrastructure.Services.Files;
using MngDataGateway.Tests.Helpers;
using System.Text;

namespace MngDataGateway.Tests.Services.Files;

/// <summary>
/// Unit tests for FileCompressionService
/// </summary>
public class FileCompressionServiceTests
{
    private readonly FileCompressionService _compressionService;

    public FileCompressionServiceTests()
    {
        var logger = LoggerMockHelper.CreateMockLogger<FileCompressionService>();
        _compressionService = new FileCompressionService(logger, 6);
    }

    #region Compression Tests

    [Fact]
    public async Task CompressAsync_ValidData_Success()
    {
        // Arrange
        var originalData = Encoding.UTF8.GetBytes("Hello, World! Hello, World! Hello, World!");

        // Act
        var result = await _compressionService.CompressAsync(originalData);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsCompressed);
        Assert.NotEmpty(result.Data);
        Assert.Less(result.Data.Length, originalData.Length);  // Should be smaller
        Assert.InRange(result.CompressionRatio, 0, 1);  // Ratio should be less than 1
    }

    [Fact]
    public async Task CompressAsync_EmptyData_Skipped()
    {
        // Act
        var result = await _compressionService.CompressAsync(Array.Empty<byte>());

        // Assert
        Assert.NotNull(result);
        Assert.False(result.IsCompressed);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task CompressAsync_LargeData_Success()
    {
        // Arrange
        var originalData = new byte[1024 * 1024];  // 1MB
        new Random().NextBytes(originalData);

        // Act
        var result = await _compressionService.CompressAsync(originalData);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsCompressed);
        Assert.NotEmpty(result.Data);
    }

    #endregion

    #region Decompression Tests

    [Fact]
    public async Task CompressDecompress_RoundTrip_Success()
    {
        // Arrange
        var originalData = Encoding.UTF8.GetBytes("Hello, World! This is a test for compression and decompression.");
        var compressionResult = await _compressionService.CompressAsync(originalData);

        // Act
        var decompressed = await _compressionService.DecompressAsync(compressionResult.Data);

        // Assert
        Assert.Equal(originalData, decompressed);
    }

    [Fact]
    public async Task DecompressAsync_InvalidData_Throws()
    {
        // Arrange
        var invalidData = new byte[] { 0x00, 0x01, 0x02, 0x03 };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _compressionService.DecompressAsync(invalidData));
    }

    [Fact]
    public async Task DecompressAsync_EmptyData_Throws()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _compressionService.DecompressAsync(Array.Empty<byte>()));
    }

    #endregion

    #region Gzip Detection Tests

    [Fact]
    public void IsGzipCompressed_GzipData_True()
    {
        // Arrange
        var gzipData = new byte[] { 0x1F, 0x8B, 0x08, 0x00 };  // Gzip magic bytes

        // Act
        var result = _compressionService.IsGzipCompressed(gzipData);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsGzipCompressed_RandomData_False()
    {
        // Arrange
        var randomData = new byte[] { 0x00, 0x01, 0x02, 0x03 };

        // Act
        var result = _compressionService.IsGzipCompressed(randomData);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsGzipCompressed_EmptyData_False()
    {
        // Act
        var result = _compressionService.IsGzipCompressed(Array.Empty<byte>());

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsGzipCompressed_SingleByte_False()
    {
        // Act
        var result = _compressionService.IsGzipCompressed(new byte[] { 0x1F });

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Compression Ratio Tests

    [Fact]
    public async Task CompressAsync_HighlyRepetitiveData_GoodRatio()
    {
        // Arrange
        var repetitiveData = new byte[10000];
        Array.Fill(repetitiveData, (byte)'A');  // All A's - highly compressible

        // Act
        var result = await _compressionService.CompressAsync(repetitiveData);

        // Assert
        Assert.True(result.IsCompressed);
        Assert.Less(result.CompressionRatio, 0.1);  // Should be very small ratio
    }

    [Fact]
    public async Task CompressAsync_RandomData_LowerRatio()
    {
        // Arrange
        var randomData = new byte[10000];
        new Random().NextBytes(randomData);

        // Act
        var result = await _compressionService.CompressAsync(randomData);

        // Assert
        // Random data doesn't compress well
        Assert.True(result.IsCompressed);
        Assert.InRange(result.CompressionRatio, 0.8, 1.5);  // Should be close to 1.0
    }

    #endregion
}
