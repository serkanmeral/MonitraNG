using Microsoft.Extensions.Logging;
using MngKeeper.Application.Interfaces;

namespace MngKeeper.Application.Pipelines.DomainCreation.Steps;

/// <summary>
/// Step 11: Create trial license for domain
/// Executes after CreateMinIOBucketStep to ensure bucket exists
/// </summary>
public class CreateLicenseStep : IPipelineStep<DomainCreationContext>
{
    private readonly ILogger<CreateLicenseStep> _logger;
    private readonly ILicenseService _licenseService;
    
    public string StepName => "CreateLicense";
    
    public CreateLicenseStep(
        ILogger<CreateLicenseStep> logger,
        ILicenseService licenseService)
    {
        _logger = logger;
        _licenseService = licenseService;
    }
    
    public async Task<StepResult> ExecuteAsync(
        DomainCreationContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Creating trial license for domain: {DomainName}", context.DomainName);
            
            // Create 15-day trial license
            var licenseInfo = await _licenseService.CreateTrialLicenseAsync(
                context.DomainName, 
                days: 15, 
                cancellationToken);
            
            _logger.LogInformation("Trial license created successfully for domain: {DomainName}, expires at: {ExpiresAt}", 
                context.DomainName, 
                licenseInfo.TrialLicenseExpiresAt);
            
            return StepResult.Success(new Dictionary<string, object>
            {
                ["licenseType"] = "Trial",
                ["expiresAt"] = licenseInfo.TrialLicenseExpiresAt?.ToString("O") ?? "",
                ["status"] = "created"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create trial license for domain: {DomainName}", context.DomainName);
            
            // Non-critical: domain can still work without license (will be handled by validation)
            return StepResult.Success(new Dictionary<string, object>
            {
                ["warning"] = $"Trial license creation failed: {ex.Message}"
            });
        }
    }
    
    public async Task RollbackAsync(
        DomainCreationContext context, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Rollback: Removing trial license for domain {DomainName}", context.DomainName);
        
        try
        {
            // Note: License files are stored in MinIO, but we don't delete them on rollback
            // as the bucket might be deleted separately. This is a soft rollback.
            _logger.LogInformation("License rollback completed (files remain in MinIO)");
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rollback license creation for domain: {DomainName}", context.DomainName);
        }
    }
}
