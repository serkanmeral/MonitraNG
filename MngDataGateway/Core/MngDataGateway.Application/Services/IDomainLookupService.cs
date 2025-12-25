using System.Threading.Tasks;

namespace MngDataGateway.Application.Services;

/// <summary>
/// Domain lookup service - DomainId'den domain name ve database name'i alır
/// </summary>
public interface IDomainLookupService
{
    /// <summary>
    /// Get domain name from domainId
    /// </summary>
    /// <param name="domainId">Domain ObjectId (MngKeeper)</param>
    /// <returns>Domain name (e.g., "seven", "proline") or null if not found</returns>
    Task<string?> GetDomainNameAsync(string domainId);

    /// <summary>
    /// Get database name from domainId
    /// </summary>
    /// <param name="domainId">Domain ObjectId (MngKeeper)</param>
    /// <returns>Database name (e.g., "mng_seven", "mng_proline") or null if not found</returns>
    Task<string?> GetDatabaseNameAsync(string domainId);

    /// <summary>
    /// Clear cache (if caching is enabled)
    /// </summary>
    Task ClearCacheAsync();
}

