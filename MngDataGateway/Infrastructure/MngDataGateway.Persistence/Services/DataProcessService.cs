using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MngDataGateway.Application.Services;
using MngDataGateway.Domain.Entities;

namespace MngDataGateway.Persistence.Services
{
    /// <summary>
    /// Data processing service implementation
    /// </summary>
    public class DataProcessService : IDataProcessService
    {
        private readonly ILogger<DataProcessService> _logger;
        private readonly IMongoClient _mongoClient;
        private readonly IIncrementalFieldService _incrementalFieldService;
        private readonly HashSet<string> _createdCollections = new();
        private readonly object _lock = new();

        public DataProcessService(
            ILogger<DataProcessService> logger,
            IMongoClient mongoClient,
            IIncrementalFieldService incrementalFieldService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mongoClient = mongoClient ?? throw new ArgumentNullException(nameof(mongoClient));
            _incrementalFieldService = incrementalFieldService ?? throw new ArgumentNullException(nameof(incrementalFieldService));
        }

        public void ApplyDefaultValues(DatasetSchema schema, Dictionary<string, object> data)
        {
            foreach (var field in schema.fields)
            {
                // Skip if field already has value
                if (data.ContainsKey(field.name) && data[field.name] != null)
                    continue;

                // Skip incremental fields (auto-generated)
                if (field.fieldType == "incremental")
                    continue;

                // Apply default value if defined (use defaultValueBson from MongoDB)
                if (field.defaultValueBson != null && !field.defaultValueBson.IsBsonNull)
                {
                    data[field.name] = BsonTypeMapper.MapToDotNetValue(field.defaultValueBson);
                    
                    _logger.LogDebug(
                        "Applied default value for field {FieldName}: {DefaultValue}",
                        field.name, data[field.name]);
                }
            }
        }

        public async Task GenerateIncrementalFieldsAsync(
            DatasetSchema schema,
            Dictionary<string, object> data,
            string databaseName,
            string? domainName = null,
            IClientSessionHandle? session = null)
        {
            var incrementalFields = schema.fields.Where(f => f.fieldType == "incremental").ToList();
            
            if (!incrementalFields.Any())
                return;

            foreach (var field in incrementalFields)
            {
                try
                {
                    var value = await _incrementalFieldService.GenerateValueAsync(
                        schema, field, data, databaseName, domainName, session);
                    
                    data[field.name] = value;
                    
                    _logger.LogDebug(
                        "Generated incremental field {FieldName}: {Value}",
                        field.name, value);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to generate incremental field {FieldName} in dataset {DatasetName}",
                        field.name, schema.DatasetName);
                    throw;
                }
            }
        }

        public void GenerateMetadata(
            DatasetSchema schema,
            Dictionary<string, object> data,
            string userId,
            string userEmail,
            string? ipAddress = null)
        {
            // Always generate __dataId
            if (!data.ContainsKey("__dataId"))
            {
                data["__dataId"] = Guid.NewGuid().ToString();
            }

            // Generate __history if logging mode is "self"
            if (schema.logging == "self")
            {
                var historyEntry = new Dictionary<string, object>
                {
                    { "operation", "create" },
                    { "userId", userId },
                    { "userEmail", userEmail },
                    { "timestamp", DateTime.UtcNow },
                    { "ipAddress", ipAddress ?? string.Empty },
                    { "changes", null! }
                };

                data["__history"] = new List<Dictionary<string, object>> { historyEntry };
                
                _logger.LogDebug(
                    "Generated metadata for dataset {DatasetName} with logging mode 'self'",
                    schema.DatasetName);
            }
            else
            {
                _logger.LogDebug(
                    "Generated metadata for dataset {DatasetName} without history (logging: {LoggingMode})",
                    schema.DatasetName, schema.logging);
            }
        }

        public async Task EnsureCollectionAndIndexesAsync(
            DatasetSchema schema,
            string databaseName)
        {
            var collectionName = schema.CollectionName;
            var cacheKey = $"{databaseName}.{collectionName}";

            // Check cache first (avoid repeated checks)
            if (_createdCollections.Contains(cacheKey))
            {
                _logger.LogDebug("Collection {CollectionName} already verified", collectionName);
                return;
            }

            try
            {
                var database = _mongoClient.GetDatabase(databaseName);
                
                // Check if collection exists
                var collectionList = await database.ListCollectionNamesAsync();
                var collections = await collectionList.ToListAsync();
                var exists = collections.Contains(collectionName);

                if (!exists)
                {
                    _logger.LogInformation("Creating collection: {CollectionName}", collectionName);
                    await database.CreateCollectionAsync(collectionName);
                }

                // Create indexes if defined
                if (schema.indexList != null && schema.indexList.Any())
                {
                    await CreateIndexesAsync(database, collectionName, schema.indexList);
                }

                // Add to cache
                lock (_lock)
                {
                    _createdCollections.Add(cacheKey);
                }

                _logger.LogInformation(
                    "Collection and indexes verified for {CollectionName} in database {DatabaseName}",
                    collectionName, databaseName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to ensure collection and indexes for {CollectionName} in database {DatabaseName}",
                    collectionName, databaseName);
                throw;
            }
        }

        private async Task CreateIndexesAsync(
            IMongoDatabase database,
            string collectionName,
            List<IndexDefinition> indexDefinitions)
        {
            var collection = database.GetCollection<BsonDocument>(collectionName);

            foreach (var indexDef in indexDefinitions)
            {
                try
                {
                    var indexKeysBuilder = Builders<BsonDocument>.IndexKeys;
                    IndexKeysDefinition<BsonDocument>? indexKeys = null;

                    // Build compound index
                    var keysList = new List<IndexKeysDefinition<BsonDocument>>();
                    
                    foreach (var field in indexDef.fields)
                    {
                        if (field.Value == 1)
                            keysList.Add(indexKeysBuilder.Ascending(field.Key));
                        else if (field.Value == -1)
                            keysList.Add(indexKeysBuilder.Descending(field.Key));
                    }

                    if (keysList.Any())
                    {
                        indexKeys = keysList.Count == 1 
                            ? keysList[0] 
                            : indexKeysBuilder.Combine(keysList);

                        var indexOptions = new CreateIndexOptions
                        {
                            Name = indexDef.name,
                            Unique = indexDef.unique,
                            Sparse = indexDef.sparse
                        };

                        var indexModel = new CreateIndexModel<BsonDocument>(indexKeys, indexOptions);
                        
                        await collection.Indexes.CreateOneAsync(indexModel);
                        
                        _logger.LogInformation(
                            "Created index '{IndexName}' on collection {CollectionName}",
                            indexDef.name, collectionName);
                    }
                }
                catch (MongoCommandException ex) when (ex.CodeName == "IndexOptionsConflict" || ex.CodeName == "IndexAlreadyExists")
                {
                    _logger.LogDebug(
                        "Index '{IndexName}' already exists on collection {CollectionName}",
                        indexDef.name, collectionName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to create index '{IndexName}' on collection {CollectionName}",
                        indexDef.name, collectionName);
                    // Don't throw - continue with other indexes
                }
            }
        }
    }
}

