namespace MngDataGateway.Application.Services.Files;

/// <summary>
/// File encryption service interface
/// Handles AES-256-GCM encryption and decryption
/// </summary>
public interface IFileEncryptionService
{
    /// <summary>
    /// Encrypts data using AES-256-GCM
    /// </summary>
    /// <param name="plainData">Data to encrypt</param>
    /// <returns>Encryption result containing nonce, tag, and ciphertext</returns>
    /// <remarks>
    /// Uses random nonce (96-bit) and 128-bit authentication tag
    /// Result format: nonce (12 bytes) + tag (16 bytes) + ciphertext
    /// </remarks>
    Task<byte[]> EncryptAsync(byte[] plainData);

    /// <summary>
    /// Decrypts AES-256-GCM encrypted data
    /// </summary>
    /// <param name="encryptedData">Encrypted data (nonce + tag + ciphertext)</param>
    /// <returns>Decrypted plaintext</returns>
    /// <exception cref="CryptographicException">If decryption fails or authentication fails</exception>
    Task<byte[]> DecryptAsync(byte[] encryptedData);

    /// <summary>
    /// Gets encryption configuration info
    /// </summary>
    /// <returns>Encryption configuration details</returns>
    EncryptionInfo GetEncryptionInfo();
}

/// <summary>
/// Encryption configuration and info
/// </summary>
public class EncryptionInfo
{
    public string Algorithm { get; set; } = "AES-256-GCM";
    public int KeySizeBits { get; set; } = 256;
    public int NonceSizeBytes { get; set; } = 12;  // 96 bits
    public int AuthenticationTagSizeBytes { get; set; } = 16;  // 128 bits
    public string KeyDerivation { get; set; } = "None";  // Built-in key
}
