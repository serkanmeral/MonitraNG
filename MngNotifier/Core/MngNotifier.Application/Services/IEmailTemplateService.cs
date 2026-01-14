namespace MngNotifier.Application.Services;

/// <summary>
/// Service for reading email templates and replacing placeholders
/// </summary>
public interface IEmailTemplateService
{
    /// <summary>
    /// Reads an email template file and replaces placeholders with provided values
    /// </summary>
    /// <param name="templateName">Template file name (without path, e.g., "domain-created")</param>
    /// <param name="placeholders">Dictionary of placeholder keys and values (e.g., {{DomainName}} -> "meral")</param>
    /// <returns>Processed HTML content with placeholders replaced</returns>
    Task<string> GetTemplateContentAsync(string templateName, Dictionary<string, string> placeholders);
}
