using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace MngKeeper.Application.Pipelines.DomainCreation.Steps;

/// <summary>
/// Step 3.5: Initialize default collections in the domain database
/// Creates @datasets and @dataset_categories collections
/// </summary>
public class InitializeDatabaseCollectionsStep : IPipelineStep<DomainCreationContext>
{
    private readonly IMongoClient _mongoClient;
    private readonly ILogger<InitializeDatabaseCollectionsStep> _logger;
    
    public string StepName => "InitializeDatabaseCollections";
    
    public InitializeDatabaseCollectionsStep(
        IMongoClient mongoClient,
        ILogger<InitializeDatabaseCollectionsStep> logger)
    {
        _mongoClient = mongoClient;
        _logger = logger;
    }
    
    public async Task<StepResult> ExecuteAsync(
        DomainCreationContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Initializing default collections in database: {DatabaseName}", context.DatabaseName);
            
            var database = _mongoClient.GetDatabase(context.DatabaseName);
            
            // Create @datasets collection
            await database.CreateCollectionAsync("@datasets", cancellationToken: cancellationToken);
            _logger.LogInformation("Created collection: @datasets");
            
            // Create @dataset_categories collection
            await database.CreateCollectionAsync("@dataset_categories", cancellationToken: cancellationToken);
            _logger.LogInformation("Created collection: @dataset_categories");
            
            // Create indexes for better performance
            var datasetsCollection = database.GetCollection<MongoDB.Bson.BsonDocument>("@datasets");
            await datasetsCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<MongoDB.Bson.BsonDocument>(
                    Builders<MongoDB.Bson.BsonDocument>.IndexKeys.Ascending("name"),
                    new CreateIndexOptions { Unique = true }
                ),
                cancellationToken: cancellationToken
            );
            _logger.LogInformation("Created index on @datasets.name");
            
            var categoriesCollection = database.GetCollection<MongoDB.Bson.BsonDocument>("@dataset_categories");
            await categoriesCollection.Indexes.CreateOneAsync(
                new CreateIndexModel<MongoDB.Bson.BsonDocument>(
                    Builders<MongoDB.Bson.BsonDocument>.IndexKeys.Ascending("name"),
                    new CreateIndexOptions { Unique = true }
                ),
                cancellationToken: cancellationToken
            );
            _logger.LogInformation("Created index on @dataset_categories.name");
            
            _logger.LogInformation("Default collections initialized successfully in: {DatabaseName}", context.DatabaseName);
            
            return StepResult.Success(new Dictionary<string, object>
            {
                ["collectionsCreated"] = new[] { "@datasets", "@dataset_categories" }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize default collections in: {DatabaseName}", context.DatabaseName);
            return StepResult.Failure($"Failed to initialize collections: {ex.Message}", ex);
        }
    }
    
    public async Task RollbackAsync(
        DomainCreationContext context, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Rollback: Dropping collections in {DatabaseName}", context.DatabaseName);
        
        try
        {
            var database = _mongoClient.GetDatabase(context.DatabaseName);
            
            await database.DropCollectionAsync("@datasets", cancellationToken);
            await database.DropCollectionAsync("@dataset_categories", cancellationToken);
            
            _logger.LogInformation("Collections dropped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to drop collections during rollback");
        }
    }
}

