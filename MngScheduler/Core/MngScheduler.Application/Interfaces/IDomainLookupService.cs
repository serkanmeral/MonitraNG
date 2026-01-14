namespace MngScheduler.Application.Interfaces;

/// <summary>
/// Service for looking up domain information
/// Used to get active domains for User Job synchronization
/// </summary>
public interface IDomainLookupService
{
    /// <summary>
    /// Get all active domains
    /// </summary>
    Task<IEnumerable<DomainInfo>> GetActiveDomainsAsync();

    /// <summary>
    /// Get domain by ID
    /// </summary>
    Task<DomainInfo?> GetDomainByIdAsync(string domainId);

    /// <summary>
    /// Get domain database name by domain ID
    /// </summary>
    Task<string?> GetDatabaseNameAsync(string domainId);
}

/// <summary>
/// Domain information DTO
/// </summary>
public class DomainInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
