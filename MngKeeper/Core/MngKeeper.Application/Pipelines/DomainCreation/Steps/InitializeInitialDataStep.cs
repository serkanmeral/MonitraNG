using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MngKeeper.Application.Interfaces;
using MngKeeper.Application.DTOs;

namespace MngKeeper.Application.Pipelines.DomainCreation.Steps;

/// <summary>
/// Step 4.5: Initialize initial data from template
/// If TemplateName is provided, uses template service to load from MinIO
/// Otherwise, falls back to legacy mng_templates database (backward compatibility)
/// No modifications are made to the data - everything is copied as-is
/// </summary>
public class InitializeInitialDataStep : IPipelineStep<DomainCreationContext>
{
    private const string TemplateDatabaseName = "mng_templates";
    private readonly IMongoClient _mongoClient;
    private readonly ITemplateService _templateService;
    private readonly ILogger<InitializeInitialDataStep> _logger;
    
    // System collections that should not be copied (already created by other steps)
    private static readonly string[] SystemCollections = { "@users", "@groups" };
    
    public string StepName => "InitializeInitialData";
    
    public InitializeInitialDataStep(
        IMongoClient mongoClient,
        ITemplateService templateService,
        ILogger<InitializeInitialDataStep> logger)
    {
        _mongoClient = mongoClient;
        _templateService = templateService;
        _logger = logger;
    }
    
    public async Task<StepResult> ExecuteAsync(
        DomainCreationContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            // If template name is provided, use template service
            if (!string.IsNullOrWhiteSpace(context.TemplateName))
            {
                _logger.LogInformation("Initializing initial data from template: {TemplateName}", context.TemplateName);
                return await InitializeFromTemplateAsync(context, cancellationToken);
            }
            
            // Fallback to legacy mng_templates database (backward compatibility)
            _logger.LogInformation("No template name provided, using legacy template database: {TemplateDatabase}", TemplateDatabaseName);
            return await InitializeFromLegacyDatabaseAsync(context, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize initial data");
            return StepResult.Failure($"Failed to initialize initial data: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Initialize from template service (MinIO)
    /// </summary>
    private async Task<StepResult> InitializeFromTemplateAsync(
        DomainCreationContext context,
        CancellationToken cancellationToken)
    {
        // Get template
        var template = await _templateService.GetTemplateAsync(context.TemplateName!, cancellationToken);
        if (template == null)
        {
            _logger.LogWarning("Template '{TemplateName}' not found, skipping initial data", context.TemplateName);
            return StepResult.Success(new Dictionary<string, object>
            {
                ["collectionsCopied"] = Array.Empty<string>(),
                ["documentsCopied"] = 0,
                ["templateName"] = context.TemplateName
            });
        }

        // Get template content from MinIO
        var templateContent = await _templateService.GetTemplateContentAsync(context.TemplateName!, cancellationToken);
        if (templateContent == null)
        {
            _logger.LogWarning("Template content for '{TemplateName}' not found in MinIO, skipping initial data", context.TemplateName);
            return StepResult.Success(new Dictionary<string, object>
            {
                ["collectionsCopied"] = Array.Empty<string>(),
                ["documentsCopied"] = 0,
                ["templateName"] = context.TemplateName
            });
        }

        _logger.LogInformation(
            "Found template '{TemplateName}' with {CollectionCount} collections, {DocumentCount} total documents",
            context.TemplateName, templateContent.Collections.Count, template.TotalDocumentCount);

        // Copy collections from template content
        var targetDb = _mongoClient.GetDatabase(context.DatabaseName);
        var copiedCollections = new List<string>();
        var totalDocuments = 0;

        foreach (var collectionData in templateContent.Collections)
        {
            // Skip system collections
            if (SystemCollections.Contains(collectionData.CollectionName))
            {
                _logger.LogDebug("Skipping system collection: {CollectionName}", collectionData.CollectionName);
                continue;
            }

            try
            {
                var documentCount = await CopyCollectionFromTemplateContentAsync(
                    targetDb,
                    collectionData,
                    cancellationToken);

                if (documentCount > 0)
                {
                    copiedCollections.Add(collectionData.CollectionName);
                    totalDocuments += documentCount;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to copy collection {CollectionName}, continuing with other collections", collectionData.CollectionName);
                // Continue with other collections even if one fails
            }
        }

        _logger.LogInformation(
            "Initial data initialization completed from template '{TemplateName}': {CollectionCount} collections, {DocumentCount} documents copied",
            context.TemplateName, copiedCollections.Count, totalDocuments);

        // Store in context metadata
        context.Metadata["initialDataCollections"] = copiedCollections;
        context.Metadata["initialDataDocumentsCount"] = totalDocuments;
        context.Metadata["templateName"] = context.TemplateName;

        return StepResult.Success(new Dictionary<string, object>
        {
            ["collectionsCopied"] = copiedCollections,
            ["documentsCopied"] = totalDocuments,
            ["templateName"] = context.TemplateName
        });
    }

    /// <summary>
    /// Initialize from legacy mng_templates database (backward compatibility)
    /// </summary>
    private async Task<StepResult> InitializeFromLegacyDatabaseAsync(
        DomainCreationContext context,
        CancellationToken cancellationToken)
    {
        // Check if template database exists
        var templateDb = _mongoClient.GetDatabase(TemplateDatabaseName);
        var collections = await templateDb.ListCollectionNamesAsync(cancellationToken: cancellationToken);
        var collectionList = await collections.ToListAsync(cancellationToken);
        
        if (collectionList.Count == 0)
        {
            _logger.LogInformation("Template database {TemplateDatabase} is empty or does not exist, skipping initial data", TemplateDatabaseName);
            return StepResult.Success(new Dictionary<string, object>
            {
                ["collectionsCopied"] = Array.Empty<string>(),
                ["documentsCopied"] = 0
            });
        }
        
        // Filter out system collections
        var templateCollections = collectionList
            .Where(c => !SystemCollections.Contains(c))
            .ToList();
        
        if (templateCollections.Count == 0)
        {
            _logger.LogInformation("No template collections to copy (only system collections found), skipping initial data");
            return StepResult.Success(new Dictionary<string, object>
            {
                ["collectionsCopied"] = Array.Empty<string>(),
                ["documentsCopied"] = 0
            });
        }
        
        _logger.LogInformation("Found {Count} template collections to copy: {Collections}", 
            templateCollections.Count, string.Join(", ", templateCollections));
        
        // Copy each collection
        var targetDb = _mongoClient.GetDatabase(context.DatabaseName);
        var copiedCollections = new List<string>();
        var totalDocuments = 0;
        
        foreach (var collectionName in templateCollections)
        {
            try
            {
                var documentCount = await CopyCollectionAsync(
                    templateDb, 
                    targetDb, 
                    collectionName, 
                    cancellationToken);
                
                if (documentCount > 0)
                {
                    copiedCollections.Add(collectionName);
                    totalDocuments += documentCount;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to copy collection {CollectionName}, continuing with other collections", collectionName);
                // Continue with other collections even if one fails
            }
        }
        
        _logger.LogInformation(
            "Initial data initialization completed: {CollectionCount} collections, {DocumentCount} documents copied",
            copiedCollections.Count, totalDocuments);
        
        // Store in context metadata
        context.Metadata["initialDataCollections"] = copiedCollections;
        context.Metadata["initialDataDocumentsCount"] = totalDocuments;
        
        return StepResult.Success(new Dictionary<string, object>
        {
            ["collectionsCopied"] = copiedCollections,
            ["documentsCopied"] = totalDocuments
        });
    }
    
    /// <summary>
    /// Copy collection from template content (MinIO JSON)
    /// </summary>
    private async Task<int> CopyCollectionFromTemplateContentAsync(
        IMongoDatabase targetDb,
        CollectionData collectionData,
        CancellationToken cancellationToken)
    {
        var targetCollection = targetDb.GetCollection<BsonDocument>(collectionData.CollectionName);
        
        if (collectionData.Documents.Count == 0)
        {
            _logger.LogDebug("Collection {CollectionName} is empty, skipping", collectionData.CollectionName);
            return 0;
        }

        _logger.LogInformation("Copying {Count} documents from template collection {CollectionName}", 
            collectionData.Documents.Count, collectionData.CollectionName);

        // Convert Dictionary to BsonDocument and insert
        const int BatchSize = 1000;
        var totalInserted = 0;

        for (int i = 0; i < collectionData.Documents.Count; i += BatchSize)
        {
            var batch = collectionData.Documents
                .Skip(i)
                .Take(BatchSize)
                .Select(doc => BsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(doc)))
                .ToList();

            await targetCollection.InsertManyAsync(batch, cancellationToken: cancellationToken);
            totalInserted += batch.Count;
            _logger.LogDebug("Inserted batch {BatchNumber} ({Count} documents) into {CollectionName}",
                (i / BatchSize) + 1, batch.Count, collectionData.CollectionName);
        }

        // Copy indexes if available
        if (collectionData.Indexes != null && collectionData.Indexes.Count > 0)
        {
            await CopyIndexesFromTemplateContentAsync(targetCollection, collectionData.Indexes, cancellationToken);
        }

        _logger.LogInformation(
            "Copied {Count} documents from template collection {CollectionName} to target database (no modifications made)",
            totalInserted, collectionData.CollectionName);

        return totalInserted;
    }

    /// <summary>
    /// Copy indexes from template content
    /// </summary>
    private async Task CopyIndexesFromTemplateContentAsync(
        IMongoCollection<BsonDocument> targetCollection,
        List<IndexDefinition> indexes,
        CancellationToken cancellationToken)
    {
        foreach (var indexDef in indexes)
        {
            try
            {
                var keys = BsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(indexDef.Keys));
                var options = new CreateIndexOptions
                {
                    Unique = indexDef.Unique,
                    Sparse = indexDef.Sparse,
                    Background = indexDef.Background,
                    Name = indexDef.Name
                };

                var indexModel = new CreateIndexModel<BsonDocument>(keys, options);
                await targetCollection.Indexes.CreateOneAsync(indexModel, cancellationToken: cancellationToken);

                _logger.LogDebug("Copied index {IndexName} to target collection", indexDef.Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to copy index {IndexName}, continuing", indexDef.Name);
            }
        }
    }

    /// <summary>
    /// Copies a collection from source database to target database (legacy method)
    /// Documents are copied as-is without any modifications
    /// </summary>
    private async Task<int> CopyCollectionAsync(
        IMongoDatabase sourceDb,
        IMongoDatabase targetDb,
        string collectionName,
        CancellationToken cancellationToken)
    {
        var sourceCollection = sourceDb.GetCollection<BsonDocument>(collectionName);
        var targetCollection = targetDb.GetCollection<BsonDocument>(collectionName);
        
        // Get all documents from source collection
        var sourceDocuments = await sourceCollection
            .Find(FilterDefinition<BsonDocument>.Empty)
            .ToListAsync(cancellationToken);
        
        if (sourceDocuments.Count == 0)
        {
            _logger.LogDebug("Collection {CollectionName} is empty, skipping", collectionName);
            return 0;
        }
        
        _logger.LogInformation("Copying {Count} documents from {CollectionName}", sourceDocuments.Count, collectionName);
        
        // Batch insert documents (no modifications - copy as-is)
        const int BatchSize = 1000;
        var totalInserted = 0;
        
        for (int i = 0; i < sourceDocuments.Count; i += BatchSize)
        {
            var batch = sourceDocuments.Skip(i).Take(BatchSize).ToList();
            await targetCollection.InsertManyAsync(batch, cancellationToken: cancellationToken);
            totalInserted += batch.Count;
            _logger.LogDebug("Inserted batch {BatchNumber} ({Count} documents) into {CollectionName}", 
                (i / BatchSize) + 1, batch.Count, collectionName);
        }
        
        // Copy indexes
        await CopyIndexesAsync(sourceCollection, targetCollection, cancellationToken);
        
        _logger.LogInformation(
            "Copied {Count} documents from {CollectionName} to target database (no modifications made)",
            totalInserted, collectionName);
        
        return totalInserted;
    }
    
    /// <summary>
    /// Copies indexes from source collection to target collection
    /// </summary>
    private async Task CopyIndexesAsync(
        IMongoCollection<BsonDocument> sourceCollection,
        IMongoCollection<BsonDocument> targetCollection,
        CancellationToken cancellationToken)
    {
        try
        {
            var sourceIndexes = await sourceCollection.Indexes.ListAsync(cancellationToken);
            var indexes = await sourceIndexes.ToListAsync(cancellationToken);
            
            foreach (var index in indexes)
            {
                // Skip _id index (automatically created)
                if (index["name"].AsString == "_id_")
                    continue;
                
                try
                {
                    // Get index keys
                    var keys = index["key"].AsBsonDocument;
                    
                    // Build index options
                    var options = new CreateIndexOptions();
                    
                    if (index.Contains("unique"))
                        options.Unique = index["unique"].AsBoolean;
                    
                    if (index.Contains("sparse"))
                        options.Sparse = index["sparse"].AsBoolean;
                    
                    if (index.Contains("background"))
                        options.Background = index["background"].AsBoolean;
                    
                    if (index.Contains("name"))
                        options.Name = index["name"].AsString;
                    
                    // Create index in target collection
                    var indexModel = new CreateIndexModel<BsonDocument>(keys, options);
                    await targetCollection.Indexes.CreateOneAsync(indexModel, cancellationToken: cancellationToken);
                    
                    _logger.LogDebug("Copied index {IndexName} to target collection", index["name"].AsString);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to copy index {IndexName}, continuing", index["name"].AsString);
                    // Continue with other indexes even if one fails
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to copy indexes, but documents were copied successfully");
            // Don't fail the whole operation if index copying fails
        }
    }
    
    public async Task RollbackAsync(
        DomainCreationContext context, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Rollback: Dropping collections copied from template in {DatabaseName}", context.DatabaseName);
        
        try
        {
            var database = _mongoClient.GetDatabase(context.DatabaseName);
            
            // Get list of collections that were copied
            if (context.Metadata.TryGetValue("initialDataCollections", out var collectionsObj) &&
                collectionsObj is List<string> copiedCollections)
            {
                foreach (var collectionName in copiedCollections)
                {
                    try
                    {
                        await database.DropCollectionAsync(collectionName, cancellationToken);
                        _logger.LogInformation("Dropped collection: {CollectionName}", collectionName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to drop collection {CollectionName} during rollback", collectionName);
                    }
                }
            }
            else
            {
                // If we don't have the list, try to drop template collections
                var templateDb = _mongoClient.GetDatabase(TemplateDatabaseName);
                var collections = await templateDb.ListCollectionNamesAsync(cancellationToken: cancellationToken);
                var collectionList = await collections.ToListAsync(cancellationToken);
                
                var templateCollections = collectionList
                    .Where(c => !SystemCollections.Contains(c))
                    .ToList();
                
                foreach (var collectionName in templateCollections)
                {
                    try
                    {
                        await database.DropCollectionAsync(collectionName, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to drop collection {CollectionName} during rollback", collectionName);
                    }
                }
            }
            
            _logger.LogInformation("Template collections dropped successfully during rollback");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to drop template collections during rollback");
        }
    }
}
