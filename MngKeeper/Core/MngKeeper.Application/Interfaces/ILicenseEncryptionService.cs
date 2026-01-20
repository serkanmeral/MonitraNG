namespace MngKeeper.Application.Interfaces
{
    /// <summary>
    /// Service for encrypting and decrypting license files
    /// Uses AES-256-GCM encryption with domain-specific keys
    /// </summary>
    public interface ILicenseEncryptionService
    {
        /// <summary>
        /// Encrypts license data and returns encrypted bytes
        /// </summary>
        Task<byte[]> EncryptLicenseAsync(string domainName, string licenseJson, CancellationToken cancellationToken = default);

        /// <summary>
        /// Decrypts license file and returns JSON string
        /// </summary>
        Task<string> DecryptLicenseAsync(string domainName, byte[] encryptedData, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates a signature for license data
        /// </summary>
        Task<string> GenerateSignatureAsync(string domainName, string licenseJson, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates license signature
        /// </summary>
        Task<bool> ValidateSignatureAsync(string domainName, string licenseJson, string signature, CancellationToken cancellationToken = default);
    }
}
