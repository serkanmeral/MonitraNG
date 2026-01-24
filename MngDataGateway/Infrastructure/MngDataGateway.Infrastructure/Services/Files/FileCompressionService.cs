using System.IO.Compression;
using MngDataGateway.Application.Services.Files;
using Microsoft.Extensions.Logging;

namespace MngDataGateway.Infrastructure.Services.Files;

/// <summary>
/// File compression service implementation using gzip
/// Handles optional compression with error recovery
/// </summary>
public class FileCompressionService : IFileCompressionService
{
    private readonly ILogger<FileCompressionService> _logger;
    private readonly int _compressionLevel;

    public FileCompressionService(ILogger<FileCompressionService> logger, int compressionLevel = 6)
    {
        _logger = logger;
        _compressionLevel = Math.Clamp(compressionLevel, 1, 9);
    }

    /// <summary>
    /// Compresses data using gzip
    /// On failure: returns original data marked as not compressed (non-fatal)
    /// </summary>
    public async Task<FileCompressionResult> CompressAsync(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            _logger.LogDebug("Skipping compression for empty data");
            return FileCompressionResult.Skipped(data, "Empty data");
        }

        try
        {
            using var memoryStream = new MemoryStream();
            using var gzipStream = new GZipStream(memoryStream, (CompressionMode)_compressionLevel);
            
            await gzipStream.WriteAsync(data, 0, data.Length);
            await gzipStream.FlushAsync();
            
            var compressedData = memoryStream.ToArray();
            var ratio = (double)compressedData.Length / data.Length;

            _logger.LogInformation(
                "Compression successful: {OriginalSize} → {CompressedSize} bytes ({Ratio:P})",
                data.Length,
                compressedData.Length,
                ratio);

            return FileCompressionResult.Success(compressedData, data.Length);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Compression failed (non-fatal): {Message}. Continuing without compression.",
                ex.Message);

            // Non-fatal error - continue with original data
            return FileCompressionResult.Skipped(data, ex.Message);
        }
    }

    /// <summary>
    /// Decompresses gzip data
    /// </summary>
    public async Task<byte[]> DecompressAsync(byte[] compressedData)
    {
        if (compressedData == null || compressedData.Length == 0)
            throw new ArgumentException("Compressed data cannot be empty");

        try
        {
            using var memoryStream = new MemoryStream(compressedData);
            using var gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress);
            using var decompressedStream = new MemoryStream();
            
            await gzipStream.CopyToAsync(decompressedStream);
            var decompressed = decompressedStream.ToArray();

            _logger.LogInformation(
                "Decompression successful: {CompressedSize} → {DecompressedSize} bytes",
                compressedData.Length,
                decompressed.Length);

            return decompressed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Decompression failed");
            throw new InvalidOperationException("Failed to decompress data", ex);
        }
    }

    /// <summary>
    /// Checks if data is gzip compressed using magic bytes
    /// </summary>
    public bool IsGzipCompressed(byte[] data)
    {
        if (data == null || data.Length < 2)
            return false;

        // Gzip magic bytes: 1F 8B
        return data[0] == 0x1F && data[1] == 0x8B;
    }
}
