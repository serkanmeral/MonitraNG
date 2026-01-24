using Xunit;
using MngDataGateway.Infrastructure.Services.Files;
using MngDataGateway.Tests.Helpers;
using Microsoft.Extensions.Options;
using MngDataGateway.Application.Configuration;
using System.Text;

namespace MngDataGateway.Tests.Services.Files;

/// <summary>
/// Unit tests for FileEncryptionService
/// </summary>
public class FileEncryptionServiceTests
{
    private readonly FileEncryptionService _encryptionService;
    private readonly MngDataGatewaySettings _settings;

    public FileEncryptionServiceTests()
    {
        // Create encryption settings with a test key
        var testKey = Convert.ToBase64String(new byte[32]);  // 256-bit key
        _settings = new MngDataGatewaySettings
        {
            FileStorage = new FileStorageSettings
            {
                Encryption = new EncryptionSettings
                {
                    Enabled = true,
                    Algorithm = "AES-256-GCM",
                    Key = testKey,
                    KeyDerivation = "PBKDF2"
                }
            }
        };

        var logger = LoggerMockHelper.CreateMockLogger<FileEncryptionService>();
        var options = Options.Create(_settings);
        _encryptionService = new FileEncryptionService(logger, options);
    }

    #region Encryption Tests

    [Fact]
    public async Task EncryptAsync_ValidData_Success()
    {
        // Arrange
        var plainData = Encoding.UTF8.GetBytes("Hello, World!");

        // Act
        var encryptedData = await _encryptionService.EncryptAsync(plainData);

        // Assert
        Assert.NotNull(encryptedData);
        Assert.NotEmpty(encryptedData);
        Assert.NotEqual(plainData, encryptedData);  // Should be different
        Assert.True(encryptedData.Length > plainData.Length);  // Should be larger (nonce + tag)
    }

    [Fact]
    public async Task EncryptAsync_EmptyData_Throws()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _encryptionService.EncryptAsync(Array.Empty<byte>()));
    }

    [Fact]
    public async Task EncryptAsync_LargeData_Success()
    {
        // Arrange
        var largeData = new byte[1024 * 1024];  // 1MB
        new Random().NextBytes(largeData);

        // Act
        var encryptedData = await _encryptionService.EncryptAsync(largeData);

        // Assert
        Assert.NotNull(encryptedData);
        Assert.NotEmpty(encryptedData);
    }

    #endregion

    #region Decryption Tests

    [Fact]
    public async Task EncryptDecrypt_RoundTrip_Success()
    {
        // Arrange
        var originalData = Encoding.UTF8.GetBytes("Hello, World! This is a test for encryption and decryption.");
        var encryptedData = await _encryptionService.EncryptAsync(originalData);

        // Act
        var decryptedData = await _encryptionService.DecryptAsync(encryptedData);

        // Assert
        Assert.Equal(originalData, decryptedData);
    }

    [Fact]
    public async Task DecryptAsync_InvalidData_Throws()
    {
        // Arrange
        var invalidData = new byte[] { 0x00, 0x01, 0x02, 0x03 };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _encryptionService.DecryptAsync(invalidData));
    }

    [Fact]
    public async Task DecryptAsync_TamperedData_Throws()
    {
        // Arrange
        var originalData = Encoding.UTF8.GetBytes("Hello, World!");
        var encryptedData = await _encryptionService.EncryptAsync(originalData);

        // Tamper with the data
        if (encryptedData.Length > 20)
        {
            encryptedData[20]++;  // Flip a bit
        }

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _encryptionService.DecryptAsync(encryptedData));
    }

    [Fact]
    public async Task DecryptAsync_WrongKey_Throws()
    {
        // Arrange
        var originalData = Encoding.UTF8.GetBytes("Hello, World!");
        var encryptedData = await _encryptionService.EncryptAsync(originalData);

        // Create a new service with a different key
        var differentKey = Convert.ToBase64String(new byte[32] { 1 });  // Different key
        var differentSettings = new MngDataGatewaySettings
        {
            FileStorage = new FileStorageSettings
            {
                Encryption = new EncryptionSettings
                {
                    Key = differentKey,
                    Algorithm = "AES-256-GCM"
                }
            }
        };

        var logger = LoggerMockHelper.CreateMockLogger<FileEncryptionService>();
        var options = Options.Create(differentSettings);
        var differentEncryptionService = new FileEncryptionService(logger, options);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => differentEncryptionService.DecryptAsync(encryptedData));
    }

    #endregion

    #region Multiple Encryption Tests

    [Fact]
    public async Task MultipleEncryptions_DifferentNonces_Success()
    {
        // Arrange
        var plainData = Encoding.UTF8.GetBytes("Same data");

        // Act
        var encrypted1 = await _encryptionService.EncryptAsync(plainData);
        var encrypted2 = await _encryptionService.EncryptAsync(plainData);

        // Assert
        Assert.NotNull(encrypted1);
        Assert.NotNull(encrypted2);
        // Different nonces should produce different ciphertexts
        Assert.NotEqual(encrypted1, encrypted2);

        // But both should decrypt to original
        var decrypted1 = await _encryptionService.DecryptAsync(encrypted1);
        var decrypted2 = await _encryptionService.DecryptAsync(encrypted2);
        
        Assert.Equal(plainData, decrypted1);
        Assert.Equal(plainData, decrypted2);
    }

    #endregion

    #region Encryption Info Tests

    [Fact]
    public void GetEncryptionInfo_ReturnsCorrectInfo()
    {
        // Act
        var info = _encryptionService.GetEncryptionInfo();

        // Assert
        Assert.NotNull(info);
        Assert.Equal("AES-256-GCM", info.Algorithm);
        Assert.Equal(256, info.KeySizeBits);
        Assert.Equal(12, info.NonceSizeBytes);  // 96 bits
        Assert.Equal(16, info.AuthenticationTagSizeBytes);  // 128 bits
        Assert.Equal("None", info.KeyDerivation);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task EncryptAsync_BinaryData_Success()
    {
        // Arrange
        var binaryData = new byte[256];
        for (int i = 0; i < 256; i++)
        {
            binaryData[i] = (byte)i;
        }

        // Act
        var encryptedData = await _encryptionService.EncryptAsync(binaryData);

        // Assert
        Assert.NotNull(encryptedData);
        var decrypted = await _encryptionService.DecryptAsync(encryptedData);
        Assert.Equal(binaryData, decrypted);
    }

    [Fact]
    public async Task EncryptAsync_SingleByte_Success()
    {
        // Arrange
        var singleByte = new byte[] { 42 };

        // Act
        var encryptedData = await _encryptionService.EncryptAsync(singleByte);

        // Assert
        Assert.NotNull(encryptedData);
        var decrypted = await _encryptionService.DecryptAsync(encryptedData);
        Assert.Equal(singleByte, decrypted);
    }

    #endregion
}
