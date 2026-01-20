using MngKeeper.Domain.Entities;

namespace MngKeeper.Application.DTOs.License
{
    /// <summary>
    /// Result of license validation
    /// </summary>
    public class LicenseValidationResult
    {
        public bool IsValid { get; set; }
        public bool IsExpired { get; set; }
        public LicenseType? LicenseType { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public ExpirationBehavior? ExpirationBehavior { get; set; }
        public string? ErrorMessage { get; set; }
        public LicenseFeatures? LicenseFeatures { get; set; }
    }

    /// <summary>
    /// Expiration behavior configuration
    /// </summary>
    public class ExpirationBehavior
    {
        public bool BlockTokenGeneration { get; set; }
        public bool BlockCrudOperations { get; set; }
        public bool BlockGetOperations { get; set; }
        public bool AllowReadOnly { get; set; }
        public string? CustomMessage { get; set; }
    }

    /// <summary>
    /// License features (for Real licenses)
    /// </summary>
    public class LicenseFeatures
    {
        public int MaxUsers { get; set; }
        public int MaxDomains { get; set; }
        public long MaxStorageGB { get; set; }
        public bool EnableAdvancedFeatures { get; set; }
        public string? SupportLevel { get; set; }
        public bool CountActiveUsersOnly { get; set; }
        public ActiveUserDefinition? ActiveUserDefinition { get; set; }
    }

    /// <summary>
    /// Active user definition for license counting
    /// </summary>
    public class ActiveUserDefinition
    {
        public bool IsActive { get; set; } = true;
        public int? LastLoginDays { get; set; } = 90; // null = don't check
    }
}
