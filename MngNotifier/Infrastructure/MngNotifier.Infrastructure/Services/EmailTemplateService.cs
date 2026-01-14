using System.Text;
using Microsoft.Extensions.Logging;
using MngNotifier.Application.Services;

namespace MngNotifier.Infrastructure.Services;

/// <summary>
/// Service for reading email templates and replacing placeholders
/// </summary>
public class EmailTemplateService : IEmailTemplateService
{
    private readonly ILogger<EmailTemplateService> _logger;
    private const string TemplatesFolder = "Templates/Email";

    public EmailTemplateService(ILogger<EmailTemplateService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> GetTemplateContentAsync(string templateName, Dictionary<string, string> placeholders)
    {
        if (string.IsNullOrWhiteSpace(templateName))
        {
            throw new ArgumentException("Template name cannot be null or empty", nameof(templateName));
        }

        try
        {
            // Get the base directory (where the executable is located)
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var templatePath = Path.Combine(baseDirectory, TemplatesFolder, $"{templateName}.html");

            _logger.LogDebug("Loading email template from path: {TemplatePath}", templatePath);

            if (!File.Exists(templatePath))
            {
                _logger.LogError("Template file not found: {TemplatePath}", templatePath);
                throw new FileNotFoundException($"Email template not found: {templateName}", templatePath);
            }

            // Read template content
            var templateContent = await File.ReadAllTextAsync(templatePath, Encoding.UTF8);

            // Replace placeholders
            var processedContent = ReplacePlaceholders(templateContent, placeholders);

            _logger.LogDebug("Template processed successfully: {TemplateName}, Placeholders replaced: {Count}", 
                templateName, placeholders?.Count ?? 0);

            return processedContent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading or processing email template: {TemplateName}", templateName);
            throw;
        }
    }

    /// <summary>
    /// Replaces placeholders in template content (format: {{PlaceholderName}})
    /// </summary>
    private string ReplacePlaceholders(string templateContent, Dictionary<string, string>? placeholders)
    {
        if (placeholders == null || placeholders.Count == 0)
        {
            return templateContent;
        }

        var result = new StringBuilder(templateContent);

        foreach (var placeholder in placeholders)
        {
            var placeholderKey = $"{{{{{placeholder.Key}}}}}";
            var value = placeholder.Value ?? string.Empty;
            result.Replace(placeholderKey, value);
        }

        return result.ToString();
    }
}
