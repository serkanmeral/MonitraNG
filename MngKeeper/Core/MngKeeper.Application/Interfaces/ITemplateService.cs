using MngKeeper.Application.DTOs;
using MngKeeper.Domain.Entities;

namespace MngKeeper.Application.Interfaces;

/// <summary>
/// Service interface for Template management
/// </summary>
public interface ITemplateService
{
    /// <summary>
    /// Create a new template from source domain
    /// </summary>
    Task<Template> CreateTemplateAsync(
        string templateName,
        string description,
        string sourceDomainId,
        string sourceDatabaseName,
        List<SelectedCollection> collections,
        string createdBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get template by name
    /// </summary>
    Task<Template?> GetTemplateAsync(string templateName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all templates
    /// </summary>
    Task<IEnumerable<Template>> GetAllTemplatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get templates by source domain ID
    /// </summary>
    Task<IEnumerable<Template>> GetTemplatesBySourceDomainAsync(string domainId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update template (recreate content from source domain)
    /// </summary>
    Task<Template> UpdateTemplateAsync(
        string templateName,
        string? description,
        List<SelectedCollection> collections,
        string updatedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete template
    /// </summary>
    Task<bool> DeleteTemplateAsync(string templateName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get template content from MinIO
    /// </summary>
    Task<TemplateContent?> GetTemplateContentAsync(string templateName, CancellationToken cancellationToken = default);
}
