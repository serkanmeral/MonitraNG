using Microsoft.Extensions.Logging;
using MngKeeper.Application.Interfaces;

namespace MngKeeper.Application.Pipelines.DomainCreation.Steps;

/// <summary>
/// Step 3: Create MongoDB database for the domain
/// </summary>
public class CreateDatabaseStep : IPipelineStep<DomainCreationContext>
{
    private readonly IDomainRepository _domainRepository;
    private readonly ILogger<CreateDatabaseStep> _logger;
    
    public string StepName => "CreateDatabase";
    
    public CreateDatabaseStep(
        IDomainRepository domainRepository,
        ILogger<CreateDatabaseStep> logger)
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
            _logger.LogInformation("Creating database: {DatabaseName}", context.DatabaseName);
            
            await _domainRepository.CreateDatabaseAsync(context.DatabaseName);
            
            _logger.LogInformation("Database created successfully: {DatabaseName}", context.DatabaseName);
            
            return StepResult.Success(new Dictionary<string, object>
            {
                ["databaseName"] = context.DatabaseName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create database: {DatabaseName}", context.DatabaseName);
            return StepResult.Failure($"Failed to create database: {ex.Message}", ex);
        }
    }
    
    public async Task RollbackAsync(
        DomainCreationContext context, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Rollback: Dropping database {DatabaseName}", context.DatabaseName);
        
        try
        {
            await _domainRepository.DeleteDatabaseAsync(context.DatabaseName);
            _logger.LogInformation("Database dropped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to drop database during rollback");
        }
    }
}

