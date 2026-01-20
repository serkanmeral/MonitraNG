using MngKeeper.Application.DTOs.License;
using MngKeeper.Domain.Entities;

namespace MngKeeper.Application.Interfaces
{
    /// <summary>
    /// Service for managing domain licenses
    /// </summary>
    public interface ILicenseService
    {
        /// <summary>
        /// Creates a trial license for a domain
        /// </summary>
        Task<LicenseInfo> CreateTrialLicenseAsync(string domainName, int days = 15, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates license for a domain
        /// </summary>
        Task<LicenseValidationResult> ValidateLicenseAsync(string domainName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets license information for a domain by type
        /// </summary>
        Task<LicenseData?> GetLicenseAsync(string domainName, LicenseType type, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the active license (Real > Trial priority)
        /// </summary>
        Task<LicenseData?> GetActiveLicenseAsync(string domainName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a real license for a domain with specified parameters
        /// </summary>
        Task<LicenseInfo> CreateRealLicenseAsync(
            string domainName,
            DateTime expiresAt,
            ExpirationBehavior expirationBehavior,
            LicenseFeatures licenseFeatures,
            CustomerInfo? customerInfo = null,
            LicenseMetadata? metadata = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Uploads a real license file for a domain
        /// </summary>
        Task<bool> UploadRealLicenseAsync(string domainName, Stream licenseFile, CancellationToken cancellationToken = default);

        /// <summary>
        /// Renews license expiry date
        /// </summary>
        Task<bool> RenewLicenseAsync(string domainName, DateTime newExpiryDate, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if an operation is allowed based on license
        /// </summary>
        Task<bool> IsOperationAllowedAsync(string domainName, LicenseOperation operation, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets active user count for a domain
        /// </summary>
        Task<int> GetActiveUserCountAsync(string domainName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a new user can be created based on license limits
        /// </summary>
        Task<bool> CanCreateUserAsync(string domainName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalidates the active user count cache for a domain
        /// </summary>
        Task InvalidateUserCountCacheAsync(string domainName, CancellationToken cancellationToken = default);
    }
}
