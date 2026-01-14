using Microsoft.Extensions.Logging;
using MngKeeper.Application.Interfaces;

namespace MngKeeper.Application.Pipelines.DomainCreation.Steps;

/// <summary>
/// Step: Send domain created email notification to related person
/// Non-critical step: Mail send failure should not fail the pipeline
/// </summary>
public class SendDomainCreatedEmailStep : IPipelineStep<DomainCreationContext>
{
    private readonly INotifierService _notifierService;
    private readonly ILogger<SendDomainCreatedEmailStep> _logger;
    
    public string StepName => "SendDomainCreatedEmail";
    
    public SendDomainCreatedEmailStep(
        INotifierService notifierService,
        ILogger<SendDomainCreatedEmailStep> logger)
    {
        _notifierService = notifierService;
        _logger = logger;
    }
    
    public async Task<StepResult> ExecuteAsync(
        DomainCreationContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Skip if no related person email provided
            if (string.IsNullOrWhiteSpace(context.RelatedPersonEmail))
            {
                _logger.LogInformation("Related person email not provided, skipping email notification for domain: {DomainName}", 
                    context.DomainName);
                return StepResult.Success(new Dictionary<string, object>
                {
                    ["skipped"] = true,
                    ["reason"] = "RelatedPersonEmail not provided"
                });
            }

            // Validate email format (basic validation)
            if (!IsValidEmail(context.RelatedPersonEmail))
            {
                _logger.LogWarning("Invalid email format for related person: {Email}, skipping email notification for domain: {DomainName}",
                    context.RelatedPersonEmail, context.DomainName);
                return StepResult.Success(new Dictionary<string, object>
                {
                    ["skipped"] = true,
                    ["reason"] = "Invalid email format"
                });
            }

            _logger.LogInformation("Sending domain created email notification. Domain: {DomainName}, To: {Email}",
                context.DomainName, context.RelatedPersonEmail);

            // Prepare email content
            // Note: We'll use a template service in MngNotifier, but for now we'll send a simple email
            // The template processing will be done in MngNotifier service
            var subject = $"Domain Oluşturuldu: {context.DisplayName}";
            var body = GenerateEmailBody(context);

            // Send email via MngNotifier API
            await _notifierService.SendEmailAsync(
                to: new List<string> { context.RelatedPersonEmail },
                subject: subject,
                body: body,
                isHtml: true,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Domain created email notification sent successfully. Domain: {DomainName}, To: {Email}",
                context.DomainName, context.RelatedPersonEmail);

            return StepResult.Success(new Dictionary<string, object>
            {
                ["emailSent"] = true,
                ["recipient"] = context.RelatedPersonEmail
            });
        }
        catch (Exception ex)
        {
            // Non-critical: log but don't fail the pipeline
            _logger.LogError(ex, "Failed to send domain created email notification (non-critical) for domain: {DomainName}",
                context.DomainName);
            
            // Return success even if email send fails
            // The domain is still created successfully
            return StepResult.Success(new Dictionary<string, object>
            {
                ["emailSent"] = false,
                ["warning"] = "Email notification failed but domain created",
                ["error"] = ex.Message
            });
        }
    }
    
    public Task RollbackAsync(
        DomainCreationContext context, 
        CancellationToken cancellationToken = default)
    {
        // Cannot rollback a sent email (fire-and-forget)
        _logger.LogInformation("Rollback: SendDomainCreatedEmail (no action needed - fire-and-forget)");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Generates email body with domain information
    /// Note: This is a temporary solution. In the future, we'll use template service in MngNotifier
    /// </summary>
    private string GenerateEmailBody(DomainCreationContext context)
    {
        var createdAt = context.Domain?.CreatedAt ?? DateTime.UtcNow;
        var createdAtFormatted = createdAt.ToString("dd.MM.yyyy HH:mm", System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));
        
        var adminUsername = context.AdminUser?.Username ?? $"{context.DomainName}_admin";
        var relatedPersonName = context.RelatedPersonEmail?.Split('@')[0] ?? "Değerli Müşteri";

        // For now, generate a simple HTML email
        // TODO: Use template service in MngNotifier to process domain-created.html template
        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>Domain Oluşturuldu</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333;"">
    <div style=""max-width: 600px; margin: 0 auto; padding: 20px;"">
        <h1 style=""color: #667eea;"">Domain Başarıyla Oluşturuldu</h1>
        
        <p>Merhaba <strong>{relatedPersonName}</strong>,</p>
        
        <p>MonitraNG platformunda <strong>{context.DisplayName}</strong> domain'i başarıyla oluşturulmuştur.</p>
        
        <div style=""background-color: #f8f9fa; padding: 20px; margin: 20px 0; border-left: 4px solid #667eea;"">
            <h2 style=""margin-top: 0;"">Domain Bilgileri</h2>
            <p><strong>Domain Adı:</strong> {context.DomainName}</p>
            <p><strong>Görünen Ad:</strong> {context.DisplayName}</p>
            <p><strong>Oluşturulma Tarihi:</strong> {createdAtFormatted}</p>
        </div>
        
        <div style=""background-color: #fff3cd; padding: 20px; margin: 20px 0; border: 2px solid #ffc107;"">
            <h3 style=""margin-top: 0; color: #856404;"">Yönetici Hesap Bilgileri</h3>
            <p><strong>Kullanıcı Adı:</strong> {adminUsername}</p>
            <p><strong>E-posta:</strong> {context.AdminEmail}</p>
            <p><strong>Şifre:</strong> <code style=""background-color: #fff; padding: 2px 6px; border-radius: 3px;"">{context.AdminPassword}</code></p>
            <p style=""color: #856404; font-size: 13px; margin-top: 15px; padding-top: 15px; border-top: 1px solid #ffc107; font-style: italic;"">
                ⚠️ Güvenlik Uyarısı: Bu bilgileri güvenli bir yerde saklayın. İlk giriş sonrası şifrenizi değiştirmenizi öneririz.
            </p>
        </div>
        
        <p>İyi çalışmalar dileriz,<br><strong>MonitraNG Ekibi</strong></p>
    </div>
</body>
</html>";
    }

    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}
