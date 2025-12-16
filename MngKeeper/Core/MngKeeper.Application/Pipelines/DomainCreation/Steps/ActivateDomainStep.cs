using Microsoft.Extensions.Logging;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;

namespace MngKeeper.Application.Pipelines.DomainCreation.Steps;

/// <summary>
/// Step 10: Activate domain (final step)
/// </summary>
public class ActivateDomainStep : IPipelineStep<DomainCreationContext>
{
    private readonly IDomainRepository _domainRepository;
    private readonly ILogger<ActivateDomainStep> _logger;
    
    public string StepName => "ActivateDomain";
    
    public ActivateDomainStep(
        IDomainRepository domainRepository,
        ILogger<ActivateDomainStep> logger)
    {
        _domainRepository = domainRepository;
        _logger = logger;
    }
    
    public async Task<StepResult> ExecuteAsync(
        DomainCreationContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Activating domain: {DomainName}", context.DomainName);
            
            if (context.Domain == null)
            {
                return StepResult.Failure("Domain entity not found in context");
            }
            
            // Update domain status to Active
            context.Domain.Status = DomainStatus.Active;
            context.Domain.UpdatedAt = DateTime.UtcNow;
            context.Domain.UpdatedBy = MngKeeper.Application.Common.Constants.SystemConstants.SystemUser;
            
            await _domainRepository.UpdateAsync(context.Domain);
            
            _logger.LogInformation("Domain activated successfully: {DomainName}", context.DomainName);
            
            return StepResult.Success(new Dictionary<string, object>
            {
                ["status"] = "Active",
                ["activatedAt"] = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate domain");
            return StepResult.Failure($"Failed to activate domain: {ex.Message}", ex);
        }
    }
    
    public async Task RollbackAsync(
        DomainCreationContext context, 
        CancellationToken cancellationToken = default)
    {
        if (context.Domain != null)
        {
            _logger.LogWarning("Rollback: Setting domain status to Failed");
            
            try
            {
                context.Domain.Status = DomainStatus.Failed;
                context.Domain.UpdatedAt = DateTime.UtcNow;
                context.Domain.UpdatedBy = MngKeeper.Application.Common.Constants.SystemConstants.SystemUser;
                
                await _domainRepository.UpdateAsync(context.Domain);
                
                _logger.LogInformation("Domain status set to Failed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update domain status during rollback");
            }
        }
    }
}

