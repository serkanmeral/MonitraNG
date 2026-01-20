using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MngKeeper.Application.Interfaces;
using MngKeeper.Application.DTOs.License;

namespace MngKeeper.Infrastructure.Services
{
    /// <summary>
    /// Service for encrypting and decrypting license files
    /// Uses AES-256-GCM encryption with domain-specific keys derived from master key + domain name
    /// </summary>
    public class LicenseEncryptionService : ILicenseEncryptionService
    {
        private readonly ILogger<LicenseEncryptionService> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _masterKey;

        public LicenseEncryptionService(ILogger<LicenseEncryptionService> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            
            // Get master key from configuration (should be in appsettings or environment variable)
            _masterKey = configuration["MngKeeperSettings:License:MasterKey"] 
                ?? throw new InvalidOperationException("License MasterKey is not configured. Set MngKeeperSettings:License:MasterKey in appsettings.json or environment variable.");
        }

        public async Task<byte[]> EncryptLicenseAsync(string domainName, string licenseJson, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Encrypting license for domain: {DomainName}", domainName);

                // Derive domain-specific key
                var key = DeriveKey(domainName);
                var iv = new byte[12]; // 96-bit IV for GCM
                RandomNumberGenerator.Fill(iv);

                using var aes = new AesGcm(key);
                var plaintext = Encoding.UTF8.GetBytes(licenseJson);
                var ciphertext = new byte[plaintext.Length];
                var tag = new byte[16]; // 128-bit authentication tag

                aes.Encrypt(iv, plaintext, ciphertext, tag);

                // Combine IV + ciphertext + tag
                var result = new byte[iv.Length + ciphertext.Length + tag.Length];
                Buffer.BlockCopy(iv, 0, result, 0, iv.Length);
                Buffer.BlockCopy(ciphertext, 0, result, iv.Length, ciphertext.Length);
                Buffer.BlockCopy(tag, 0, result, iv.Length + ciphertext.Length, tag.Length);

                _logger.LogDebug("License encrypted successfully for domain: {DomainName}", domainName);
                return await Task.FromResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to encrypt license for domain: {DomainName}", domainName);
                throw;
            }
        }

        public async Task<string> DecryptLicenseAsync(string domainName, byte[] encryptedData, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Decrypting license for domain: {DomainName}", domainName);

                if (encryptedData.Length < 12 + 16) // IV (12) + minimum tag (16)
                {
                    throw new ArgumentException("Encrypted data is too short", nameof(encryptedData));
                }

                // Extract IV, ciphertext, and tag
                var iv = new byte[12];
                var tag = new byte[16];
                var ciphertext = new byte[encryptedData.Length - 12 - 16];

                Buffer.BlockCopy(encryptedData, 0, iv, 0, 12);
                Buffer.BlockCopy(encryptedData, 12, ciphertext, 0, ciphertext.Length);
                Buffer.BlockCopy(encryptedData, 12 + ciphertext.Length, tag, 0, 16);

                // Derive domain-specific key
                var key = DeriveKey(domainName);

                using var aes = new AesGcm(key);
                var plaintext = new byte[ciphertext.Length];
                aes.Decrypt(iv, ciphertext, tag, plaintext);

                var result = Encoding.UTF8.GetString(plaintext);
                _logger.LogDebug("License decrypted successfully for domain: {DomainName}", domainName);
                return await Task.FromResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt license for domain: {DomainName}", domainName);
                throw;
            }
        }

        public async Task<string> GenerateSignatureAsync(string domainName, string licenseJson, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Generating signature for license: {DomainName}", domainName);

                // Use HMAC-SHA256 with domain-specific key
                var key = DeriveKey(domainName);
                using var hmac = new HMACSHA256(key);
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(licenseJson));
                var signature = Convert.ToBase64String(hash);

                _logger.LogDebug("Signature generated successfully for domain: {DomainName}", domainName);
                return await Task.FromResult(signature);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate signature for domain: {DomainName}", domainName);
                throw;
            }
        }

        public async Task<bool> ValidateSignatureAsync(string domainName, string licenseJson, string signature, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Validating signature for license: {DomainName}", domainName);

                var expectedSignature = await GenerateSignatureAsync(domainName, licenseJson, cancellationToken);
                var isValid = expectedSignature == signature;

                if (!isValid)
                {
                    _logger.LogWarning("License signature validation failed for domain: {DomainName}", domainName);
                }
                else
                {
                    _logger.LogDebug("License signature validated successfully for domain: {DomainName}", domainName);
                }

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate signature for domain: {DomainName}", domainName);
                return false;
            }
        }

        /// <summary>
        /// Derives a domain-specific encryption key from master key + domain name using PBKDF2
        /// </summary>
        private byte[] DeriveKey(string domainName)
        {
            // Use PBKDF2 to derive a 256-bit (32-byte) key
            // Salt is derived from domain name to ensure consistency
            var salt = Encoding.UTF8.GetBytes($"mng-license-salt-{domainName}");
            using var pbkdf2 = new Rfc2898DeriveBytes(
                _masterKey,
                salt,
                100000, // 100k iterations
                HashAlgorithmName.SHA256);

            return pbkdf2.GetBytes(32); // 256-bit key for AES-256
        }
    }
}
