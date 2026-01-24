namespace MngDataGateway.Application.Services.Files;

/// <summary>
/// File compression service interface
/// Handles gzip compression and decompression
/// </summary>
public interface IFileCompressionService
{
    /// <summary>
    /// Compresses data using gzip
    /// </summary>
    /// <param name="data">Original data to compress</param>
    /// <returns>Compressed data</returns>
    /// <remarks>
    /// On failure: logs warning but returns original data marked as not compressed
    /// This is non-fatal - the operation continues
    /// </remarks>
    Task<FileCompressionResult> CompressAsync(byte[] data);

    /// <summary>
    /// Decompresses gzip data
    /// </summary>
    /// <param name="compressedData">Compressed data to decompress</param>
    /// <returns>Decompressed data</returns>
    /// <exception cref="InvalidOperationException">If decompression fails</exception>
    Task<byte[]> DecompressAsync(byte[] compressedData);

    /// <summary>
    /// Checks if data is likely gzip compressed
    /// </summary>
    /// <param name="data">Data to check</param>
    /// <returns>True if gzip magic bytes detected</returns>
    bool IsGzipCompressed(byte[] data);
}

/// <summary>
/// Result of compression operation
/// </summary>
public class FileCompressionResult
{
    /// <summary>
    /// Compressed data (or original if compression failed)
    /// </summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Whether data was successfully compressed
    /// </summary>
    public bool IsCompressed { get; set; }

    /// <summary>
    /// Compression ratio (compressed size / original size)
    /// </summary>
    public double CompressionRatio { get; set; }

    /// <summary>
    /// Error message if compression failed (non-fatal)
    /// </summary>
    public string? ErrorMessage { get; set; }

    public static FileCompressionResult Success(byte[] compressedData, long originalSize)
        => new()
        {
            Data = compressedData,
            IsCompressed = true,
            CompressionRatio = (double)compressedData.Length / originalSize
        };

    public static FileCompressionResult Skipped(byte[] originalData, string reason)
        => new()
        {
            Data = originalData,
            IsCompressed = false,
            CompressionRatio = 1.0,
            ErrorMessage = reason
        };
}
