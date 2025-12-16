using Microsoft.Extensions.Logging;
using MngKeeper.Application.Services;

namespace MngKeeper.Application.Pipelines.DomainCreation.Steps;

/// <summary>
/// Step: Create MongoDB indexes for users and groups collections
/// </summary>
public class CreateIndexesStep : IPipelineStep<DomainCreationContext>
{
    private readonly IndexManager _indexManager;
    private readonly ILogger<CreateIndexesStep> _logger;
    
    public string StepName => "CreateIndexes";
    
    public CreateIndexesStep(
        IndexManager indexManager,
        ILogger<CreateIndexesStep> logger)
    {
        _indexManager = indexManager;
        _logger = logger;
    }
    
    public async Task<StepResult> ExecuteAsync(
        DomainCreationContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Creating indexes for database: {DatabaseName}", context.DatabaseName);
            
            await _indexManager.CreateAllIndexesAsync(context.DatabaseName, cancellationToken);
            
            _logger.LogInformation("Indexes created successfully for database: {DatabaseName}", context.DatabaseName);
            
            return StepResult.Success(new Dictionary<string, object>
            {
                ["indexesCreated"] = new[] { "users", "groups" }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create indexes for database: {DatabaseName}", context.DatabaseName);
            return StepResult.Failure($"Failed to create indexes: {ex.Message}", ex);
        }
    }
    
    public async Task RollbackAsync(
        DomainCreationContext context, 
        CancellationToken cancellationToken = default)
    {
        // Index rollback is not critical - indexes will be recreated on next domain creation
        _logger.LogWarning("Rollback: Index rollback skipped (non-critical)");
        await Task.CompletedTask;
    }
}

