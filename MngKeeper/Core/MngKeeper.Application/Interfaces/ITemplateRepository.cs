using MngKeeper.Domain.Entities;

namespace MngKeeper.Application.Interfaces;

/// <summary>
/// Repository interface for Template entity
/// </summary>
public interface ITemplateRepository : IRepository<Template>
{
    /// <summary>
    /// Get template by name (unique)
    /// </summary>
    Task<Template?> GetByNameAsync(string name);

    /// <summary>
    /// Check if template name exists
    /// </summary>
    Task<bool> ExistsByNameAsync(string name);

    /// <summary>
    /// Get templates by source domain ID
    /// </summary>
    Task<IEnumerable<Template>> GetBySourceDomainIdAsync(string domainId);
}
