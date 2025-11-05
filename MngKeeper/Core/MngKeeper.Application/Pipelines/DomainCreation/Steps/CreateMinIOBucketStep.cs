using Microsoft.Extensions.Logging;
using MngKeeper.Application.Interfaces;

namespace MngKeeper.Application.Pipelines.DomainCreation.Steps;

/// <summary>
/// Step 10: Create MinIO bucket for domain file storage
/// </summary>
public class CreateMinIOBucketStep : IPipelineStep<DomainCreationContext>
{
    private readonly ILogger<CreateMinIOBucketStep> _logger;
    private readonly IMinioService _minioService;
    
    public string StepName => "CreateMinIOBucket";
    
    public CreateMinIOBucketStep(
        ILogger<CreateMinIOBucketStep> logger,
        IMinioService minioService)
    {
        _logger = logger;
        _minioService = minioService;
    }
    
    public async Task<StepResult> ExecuteAsync(
        DomainCreationContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Creating MinIO bucket: {BucketName}", context.BucketName);
            
            // Create bucket
            var bucketCreated = await _minioService.CreateBucketAsync(context.BucketName, cancellationToken);
            if (!bucketCreated)
            {
                throw new Exception("Failed to create MinIO bucket");
            }
            
            // Create folder structure
            var folders = new[] { "system", "data", "backups" };
            var foldersCreated = await _minioService.CreateFolderStructureAsync(
                context.BucketName, 
                folders, 
                cancellationToken);
                
            if (!foldersCreated)
            {
                _logger.LogWarning("Failed to create folder structure, but bucket was created");
            }
            
            _logger.LogInformation("MinIO bucket created successfully: {BucketName}", context.BucketName);
            
            return StepResult.Success(new Dictionary<string, object>
            {
                ["bucketName"] = context.BucketName,
                ["foldersCreated"] = folders.Length,
                ["status"] = "created"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create MinIO bucket");
            
            // Non-critical: domain can still work without file storage
            return StepResult.Success(new Dictionary<string, object>
            {
                ["warning"] = $"MinIO bucket creation failed: {ex.Message}"
            });
        }
    }
    
    public async Task RollbackAsync(
        DomainCreationContext context, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Rollback: Deleting MinIO bucket {BucketName}", context.BucketName);
        
        try
        {
            // Note: Bucket must be empty before deletion
            // In a real rollback, we would need to delete all objects first
            await _minioService.DeleteBucketAsync(context.BucketName, cancellationToken);
            _logger.LogInformation("MinIO bucket deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete MinIO bucket during rollback");
        }
    }
}

