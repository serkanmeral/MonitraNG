using Microsoft.Extensions.Logging;
using MngKeeper.Application.Pipelines.DomainCreation.Steps;

namespace MngKeeper.Application.Pipelines.DomainCreation;

/// <summary>
/// Domain creation pipeline orchestrator
/// Executes all steps in sequence with automatic rollback on failure
/// </summary>
public class DomainCreationPipeline
{
    private readonly Pipeline<DomainCreationContext> _pipeline;
    private readonly ILogger<DomainCreationPipeline> _logger;
    
    public DomainCreationPipeline(
        ValidateDomainStep validateDomainStep,
        CreateDomainEntityStep createDomainEntityStep,
        CreateDatabaseStep createDatabaseStep,
        InitializeDatabaseCollectionsStep initializeDatabaseCollectionsStep,
        CreateKeycloakRealmStep createKeycloakRealmStep,
        CreateDefaultGroupsStep createDefaultGroupsStep,
        CreateAdminUserStep createAdminUserStep,
        PublishDomainCreatedEventStep publishDomainCreatedEventStep,
        InitializeDomainCacheStep initializeDomainCacheStep,
        CreateMinIOBucketStep createMinIOBucketStep,
        ActivateDomainStep activateDomainStep,
        ILogger<DomainCreationPipeline> logger,
        ILogger<Pipeline<DomainCreationContext>> pipelineLogger)
    {
        _logger = logger;
        _pipeline = new Pipeline<DomainCreationContext>(pipelineLogger);
        
        // Build pipeline in sequence
        _pipeline
            .AddStep(validateDomainStep)                    // Step 1
            .AddStep(createDomainEntityStep)                // Step 2
            .AddStep(createDatabaseStep)                    // Step 3
            .AddStep(initializeDatabaseCollectionsStep)     // Step 4 - Initialize Collections
            .AddStep(createKeycloakRealmStep)               // Step 5
            .AddStep(createDefaultGroupsStep)               // Step 6
            .AddStep(createAdminUserStep)                   // Step 7
            .AddStep(publishDomainCreatedEventStep)         // Step 8 - RabbitMQ
            .AddStep(initializeDomainCacheStep)             // Step 9 - Redis
            .AddStep(createMinIOBucketStep)                 // Step 10 - MinIO
            .AddStep(activateDomainStep);                   // Step 11 - Final
    }
    
    /// <summary>
    /// Execute the complete domain creation pipeline
    /// </summary>
    public async Task<PipelineResult<DomainCreationContext>> ExecuteAsync(
        DomainCreationContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting domain creation pipeline for: {DomainName}", context.DomainName);
        
        var startTime = DateTime.UtcNow;
        
        var result = await _pipeline.ExecuteAsync(context, cancellationToken);
        
        var duration = DateTime.UtcNow - startTime;
        
        if (result.IsSuccess)
        {
            _logger.LogInformation(
                "Domain creation pipeline completed successfully in {Duration}ms: {DomainName}",
                duration.TotalMilliseconds,
                context.DomainName);
        }
        else
        {
            _logger.LogError(
                "Domain creation pipeline failed at step '{FailedStep}' after {Duration}ms: {ErrorMessage}",
                result.FailedStepName,
                duration.TotalMilliseconds,
                result.ErrorMessage);
        }
        
        return result;
    }
}

