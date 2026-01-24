using System.Security.Cryptography;
using MngDataGateway.Application.Services.Files;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngDataGateway.Application.Configuration;

namespace MngDataGateway.Infrastructure.Services.Files;

/// <summary>
/// File encryption service implementation using AES-256-GCM
/// Handles encryption and decryption of file data
/// </summary>
public class FileEncryptionService : IFileEncryptionService
{
    private readonly ILogger<FileEncryptionService> _logger;
    private readonly byte[] _encryptionKey;

    // Constants for AES-GCM
    private const int NonceSize = 12;  // 96 bits
    private const int AuthenticationTagSize = 16;  // 128 bits

    public FileEncryptionService(
        ILogger<FileEncryptionService> logger,
        IOptions<MngDataGatewaySettings> options)
    {
        _logger = logger;

        var settings = options.Value.FileStorage.Encryption;

        if (string.IsNullOrEmpty(settings.Key))
            throw new InvalidOperationException(
                "Encryption key not configured. Set FileStorage:Encryption:Key in appsettings.json");

        try
        {
            _encryptionKey = Convert.FromBase64String(settings.Key);

            if (_encryptionKey.Length != 32)  // 256 bits = 32 bytes
                throw new InvalidOperationException(
                    "Encryption key must be exactly 256 bits (32 bytes)");

            _logger.LogInformation("Encryption service initialized with AES-256-GCM");
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "Invalid encryption key format. Must be base64-encoded.", ex);
        }
    }

    /// <summary>
    /// Encrypts data using AES-256-GCM with random nonce
    /// </summary>
    public async Task<byte[]> EncryptAsync(byte[] plainData)
    {
        if (plainData == null || plainData.Length == 0)
            throw new ArgumentException("Plain data cannot be empty");

        return await Task.Run(() =>
        {
            try
            {
                // Generate random nonce
                byte[] nonce = new byte[NonceSize];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(nonce);
                }

                // Create cipher and encrypt
                using var aes = new AesGcm(_encryptionKey);
                byte[] ciphertext = new byte[plainData.Length];
                byte[] tag = new byte[AuthenticationTagSize];

                aes.Encrypt(nonce, plainData, ciphertext, tag);

                // Combine: nonce + tag + ciphertext
                var result = new byte[NonceSize + AuthenticationTagSize + ciphertext.Length];
                Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
                Buffer.BlockCopy(tag, 0, result, NonceSize, AuthenticationTagSize);
                Buffer.BlockCopy(ciphertext, 0, result, NonceSize + AuthenticationTagSize, ciphertext.Length);

                _logger.LogDebug(
                    "Encryption successful: {PlainSize} → {EncryptedSize} bytes",
                    plainData.Length,
                    result.Length);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Encryption failed");
                throw new InvalidOperationException("Encryption failed", ex);
            }
        });
    }

    /// <summary>
    /// Decrypts AES-256-GCM encrypted data
    /// Format: nonce (12 bytes) + tag (16 bytes) + ciphertext
    /// </summary>
    public async Task<byte[]> DecryptAsync(byte[] encryptedData)
    {
        if (encryptedData == null || encryptedData.Length < NonceSize + AuthenticationTagSize)
            throw new ArgumentException("Invalid encrypted data format");

        return await Task.Run(() =>
        {
            try
            {
                // Parse: nonce + tag + ciphertext
                byte[] nonce = new byte[NonceSize];
                byte[] tag = new byte[AuthenticationTagSize];
                byte[] ciphertext = new byte[encryptedData.Length - NonceSize - AuthenticationTagSize];

                Buffer.BlockCopy(encryptedData, 0, nonce, 0, NonceSize);
                Buffer.BlockCopy(encryptedData, NonceSize, tag, 0, AuthenticationTagSize);
                Buffer.BlockCopy(encryptedData, NonceSize + AuthenticationTagSize, ciphertext, 0, ciphertext.Length);

                // Decrypt
                using var aes = new AesGcm(_encryptionKey);
                byte[] plaintext = new byte[ciphertext.Length];

                aes.Decrypt(nonce, ciphertext, tag, plaintext);

                _logger.LogDebug(
                    "Decryption successful: {EncryptedSize} → {PlainSize} bytes",
                    encryptedData.Length,
                    plaintext.Length);

                return plaintext;
            }
            catch (CryptographicException ex)
            {
                _logger.LogError(ex, "Decryption failed - authentication tag verification failed");
                throw new InvalidOperationException(
                    "Decryption failed - data may be corrupted or key is invalid", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Decryption failed");
                throw new InvalidOperationException("Decryption failed", ex);
            }
        });
    }

    /// <summary>
    /// Gets encryption configuration info
    /// </summary>
    public EncryptionInfo GetEncryptionInfo()
    {
        return new EncryptionInfo
        {
            Algorithm = "AES-256-GCM",
            KeySizeBits = 256,
            NonceSizeBytes = NonceSize,
            AuthenticationTagSizeBytes = AuthenticationTagSize,
            KeyDerivation = "None"  // Direct key from config
        };
    }
}
