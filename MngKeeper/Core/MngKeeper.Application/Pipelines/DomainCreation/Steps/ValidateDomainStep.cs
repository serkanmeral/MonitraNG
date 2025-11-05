using Microsoft.Extensions.Logging;
using MngKeeper.Application.Interfaces;

namespace MngKeeper.Application.Pipelines.DomainCreation.Steps;

/// <summary>
/// Step 1: Validate domain creation request
/// </summary>
public class ValidateDomainStep : IPipelineStep<DomainCreationContext>
{
    private readonly IDomainRepository _domainRepository;
    private readonly ILogger<ValidateDomainStep> _logger;
    
    public string StepName => "ValidateDomain";
    
    public ValidateDomainStep(
        IDomainRepository domainRepository,
        ILogger<ValidateDomainStep> logger)
    {
        _domainRepository = domainRepository;
        _logger = logger;
    }
    
    public async Task<StepResult> ExecuteAsync(
        DomainCreationContext context, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Validating domain: {DomainName}", context.DomainName);
        
        // 1. Check domain name format
        if (string.IsNullOrWhiteSpace(context.DomainName))
        {
            return StepResult.Failure("Domain name is required");
        }
        
        if (context.DomainName.Length < 3)
        {
            return StepResult.Failure("Domain name must be at least 3 characters");
        }
        
        if (!IsValidDomainName(context.DomainName))
        {
            return StepResult.Failure("Domain name can only contain lowercase letters, numbers, and hyphens");
        }
        
        // 2. Check duplicate
        var exists = await _domainRepository.ExistsByNameAsync(context.DomainName);
        if (exists)
        {
            return StepResult.Failure($"Domain '{context.DomainName}' already exists");
        }
        
        // 3. Validate email
        if (string.IsNullOrWhiteSpace(context.AdminEmail) || !IsValidEmail(context.AdminEmail))
        {
            return StepResult.Failure("Valid admin email is required");
        }
        
        // 4. Validate password
        if (string.IsNullOrWhiteSpace(context.AdminPassword) || context.AdminPassword.Length < 8)
        {
            return StepResult.Failure("Admin password must be at least 8 characters");
        }
        
        // 5. Compute derived values
        context.DatabaseName = $"mng_{context.DomainName.ToLower().Replace(" ", "_")}";
        context.RealmName = context.DomainName.ToLower().Replace(" ", "_");
        context.BucketName = $"mng-{context.DomainName.ToLower().Replace(" ", "-")}";
        
        _logger.LogInformation("Domain validation successful: {DomainName}", context.DomainName);
        _logger.LogInformation("Database: {DatabaseName}, Realm: {RealmName}, Bucket: {BucketName}",
            context.DatabaseName, context.RealmName, context.BucketName);
        
        return StepResult.Success(new Dictionary<string, object>
        {
            ["databaseName"] = context.DatabaseName,
            ["realmName"] = context.RealmName,
            ["bucketName"] = context.BucketName
        });
    }
    
    public Task RollbackAsync(DomainCreationContext context, CancellationToken cancellationToken = default)
    {
        // Nothing to rollback for validation
        _logger.LogInformation("Rollback: ValidateDomain (no action needed)");
        return Task.CompletedTask;
    }
    
    private bool IsValidDomainName(string domainName)
    {
        // Only lowercase letters, numbers, and hyphens
        return System.Text.RegularExpressions.Regex.IsMatch(
            domainName, 
            @"^[a-z0-9-]+$");
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

