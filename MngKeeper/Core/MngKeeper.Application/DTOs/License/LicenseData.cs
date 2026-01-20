using MngKeeper.Domain.Entities;

namespace MngKeeper.Application.DTOs.License
{
    /// <summary>
    /// License data structure (stored in encrypted file)
    /// </summary>
    public class LicenseData
    {
        public string DomainName { get; set; } = string.Empty;
        public LicenseType LicenseType { get; set; }
        public DateTime IssuedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string IssuedBy { get; set; } = string.Empty;
        public string LicenseKey { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
        public ExpirationBehavior ExpirationBehavior { get; set; } = new();
        
        // Real license only
        public CustomerInfo? CustomerInfo { get; set; }
        public LicenseFeatures? LicenseFeatures { get; set; }
        public LicenseMetadata? Metadata { get; set; }
    }

    /// <summary>
    /// Customer information (Real license only)
    /// </summary>
    public class CustomerInfo
    {
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerId { get; set; } = string.Empty;
        public string ContactEmail { get; set; } = string.Empty;
        public string? ContactPhone { get; set; }
    }

    /// <summary>
    /// License metadata (Real license only)
    /// </summary>
    public class LicenseMetadata
    {
        public DateTime? PurchaseDate { get; set; }
        public string? InvoiceNumber { get; set; }
        public string? SalesRep { get; set; }
    }
}
