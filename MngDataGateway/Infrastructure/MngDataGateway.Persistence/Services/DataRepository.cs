using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MngDataGateway.Application.Services;

namespace MngDataGateway.Persistence.Services
{
    /// <summary>
    /// Data repository implementation
    /// </summary>
    public class DataRepository : IDataRepository
    {
        private readonly ILogger<DataRepository> _logger;
        private readonly IMongoClient _mongoClient;

        public DataRepository(
            ILogger<DataRepository> logger,
            IMongoClient mongoClient)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mongoClient = mongoClient ?? throw new ArgumentNullException(nameof(mongoClient));
        }

        public async Task InsertOneAsync(
            string databaseName,
            string collectionName,
            Dictionary<string, object> data,
            IClientSessionHandle? session = null)
        {
            var database = _mongoClient.GetDatabase(databaseName);
            var collection = database.GetCollection<BsonDocument>(collectionName);

            var document = new BsonDocument(data.Select(kvp => 
                new BsonElement(kvp.Key, BsonValue.Create(kvp.Value))));

            if (session != null)
            {
                await collection.InsertOneAsync(session, document);
            }
            else
            {
                await collection.InsertOneAsync(document);
            }

            _logger.LogDebug(
                "Inserted document with __dataId: {DataId} into {Database}.{Collection}",
                data.GetValueOrDefault("__dataId"), databaseName, collectionName);
        }

        public async Task InsertManyAsync(
            string databaseName,
            string collectionName,
            List<Dictionary<string, object>> items,
            IClientSessionHandle? session = null)
        {
            if (items == null || !items.Any())
            {
                _logger.LogWarning("InsertManyAsync called with empty items list");
                return;
            }

            var database = _mongoClient.GetDatabase(databaseName);
            var collection = database.GetCollection<BsonDocument>(collectionName);

            var documents = items.Select(item => 
                new BsonDocument(item.Select(kvp => 
                    new BsonElement(kvp.Key, BsonValue.Create(kvp.Value))))).ToList();

            if (session != null)
            {
                await collection.InsertManyAsync(session, documents);
            }
            else
            {
                await collection.InsertManyAsync(documents);
            }

            _logger.LogDebug(
                "Inserted {Count} documents into {Database}.{Collection}",
                items.Count, databaseName, collectionName);
        }

        public async Task<BsonDocument?> FindByIdAsync(
            string databaseName,
            string collectionName,
            string dataId,
            bool includeDeleted = false)
        {
            var database = _mongoClient.GetDatabase(databaseName);
            var collection = database.GetCollection<BsonDocument>(collectionName);

            var filter = Builders<BsonDocument>.Filter.Eq("__dataId", dataId);

            return await collection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<(List<BsonDocument> data, long totalCount)> FindManyAsync(
            string databaseName,
            string collectionName,
            int skip = 0,
            int limit = 50,
            bool includeDeleted = false)
        {
            var database = _mongoClient.GetDatabase(databaseName);
            var collection = database.GetCollection<BsonDocument>(collectionName);

            // No soft-delete filter needed - hard delete means data is physically removed
            FilterDefinition<BsonDocument> filter = Builders<BsonDocument>.Filter.Empty;

            // Get total count
            var totalCount = await collection.CountDocumentsAsync(filter);

            // Get data with pagination
            var data = await collection
                .Find(filter)
                .Skip(skip)
                .Limit(limit)
                .ToListAsync();

            _logger.LogDebug(
                "Found {Count} documents in {Database}.{Collection} (skip: {Skip}, limit: {Limit}, total: {Total})",
                data.Count, databaseName, collectionName, skip, limit, totalCount);

            return (data, totalCount);
        }

        public async Task<bool> UpdateOneAsync(
            string databaseName,
            string collectionName,
            string dataId,
            Dictionary<string, object> updates,
            IClientSessionHandle? session = null)
        {
            var database = _mongoClient.GetDatabase(databaseName);
            var collection = database.GetCollection<BsonDocument>(collectionName);

            var filter = Builders<BsonDocument>.Filter.Eq("__dataId", dataId);

            var updateBuilder = Builders<BsonDocument>.Update;
            var updateDefinitions = updates.Select(kvp =>
                updateBuilder.Set(kvp.Key, BsonValue.Create(kvp.Value))).ToList();

            var update = updateBuilder.Combine(updateDefinitions);

            UpdateResult result;
            if (session != null)
            {
                result = await collection.UpdateOneAsync(session, filter, update);
            }
            else
            {
                result = await collection.UpdateOneAsync(filter, update);
            }

            _logger.LogDebug(
                "Updated document __dataId: {DataId} in {Database}.{Collection} (matched: {Matched}, modified: {Modified})",
                dataId, databaseName, collectionName, result.MatchedCount, result.ModifiedCount);

            return result.MatchedCount > 0;
        }

        public async Task<bool> HardDeleteAndArchiveAsync(
            string databaseName,
            string collectionName,
            string dataId,
            string userId,
            string userEmail,
            DateTime expireAt,
            string? ipAddress = null)
        {
            var database = _mongoClient.GetDatabase(databaseName);
            var collection = database.GetCollection<BsonDocument>(collectionName);
            var deletedCollection = database.GetCollection<BsonDocument>("__deletedDatas");

            // Find document to delete
            var filter = Builders<BsonDocument>.Filter.Eq("__dataId", dataId);
            var document = await collection.Find(filter).FirstOrDefaultAsync();

            if (document == null)
            {
                _logger.LogWarning(
                    "Document __dataId: {DataId} not found in {Database}.{Collection}",
                    dataId, databaseName, collectionName);
                return false;
            }

            var now = DateTime.UtcNow;
            var deletionInfo = new BsonDocument
            {
                { "userId", userId },
                { "userEmail", userEmail },
                { "timestamp", now },
                { "ipAddress", string.IsNullOrEmpty(ipAddress) ? BsonNull.Value : (BsonValue)ipAddress }
            };

            // Archive to __deletedDatas
            var archivedData = new BsonDocument
            {
                ["originalCollection"] = collectionName,
                ["deletedData"] = document,
                ["deletionInfo"] = deletionInfo,
                ["expireAt"] = expireAt
            };

            await deletedCollection.InsertOneAsync(archivedData);

            // Ensure TTL index exists on __deletedDatas
            await EnsureTTLIndexAsync(database, "__deletedDatas");

            // Hard delete from main collection
            var deleteResult = await collection.DeleteOneAsync(filter);

            _logger.LogInformation(
                "Hard-deleted and archived document __dataId: {DataId} from {Database}.{Collection} by user {UserId} (expires at {ExpireAt})",
                dataId, databaseName, collectionName, userId, expireAt);

            return deleteResult.DeletedCount > 0;
        }

        public async Task<bool> RestoreFromArchiveAsync(
            string databaseName,
            string collectionName,
            string dataId,
            string userId,
            string userEmail,
            string? ipAddress = null)
        {
            var database = _mongoClient.GetDatabase(databaseName);
            var collection = database.GetCollection<BsonDocument>(collectionName);
            var deletedCollection = database.GetCollection<BsonDocument>("__deletedDatas");

            // Find in __deletedDatas
            var filter = Builders<BsonDocument>.Filter.And(
                Builders<BsonDocument>.Filter.Eq("originalCollection", collectionName),
                Builders<BsonDocument>.Filter.Eq("deletedData.__dataId", dataId)
            );

            var archivedDoc = await deletedCollection.Find(filter).FirstOrDefaultAsync();
            if (archivedDoc == null)
            {
                _logger.LogWarning(
                    "Archived document __dataId: {DataId} not found in {Database}.__deletedDatas",
                    dataId, databaseName);
                return false;
            }

            // Check if already exists in main collection
            var existsFilter = Builders<BsonDocument>.Filter.Eq("__dataId", dataId);
            var exists = await collection.Find(existsFilter).AnyAsync();
            if (exists)
            {
                _logger.LogWarning(
                    "Document __dataId: {DataId} already exists in {Database}.{Collection}",
                    dataId, databaseName, collectionName);
                return false;
            }

            // Restore document
            var restoredDocument = archivedDoc["deletedData"].AsBsonDocument;

            // Add restore info
            var restoreInfo = new BsonDocument
            {
                { "userId", userId },
                { "userEmail", userEmail },
                { "timestamp", DateTime.UtcNow },
                { "ipAddress", string.IsNullOrEmpty(ipAddress) ? BsonNull.Value : (BsonValue)ipAddress }
            };
            restoredDocument["__restoreInfo"] = restoreInfo;

            // Insert back to main collection
            await collection.InsertOneAsync(restoredDocument);

            // Remove from __deletedDatas
            await deletedCollection.DeleteOneAsync(filter);

            _logger.LogInformation(
                "Restored document __dataId: {DataId} from archive to {Database}.{Collection} by user {UserId}",
                dataId, databaseName, collectionName, userId);

            return true;
        }

        /// <summary>
        /// Ensure TTL index exists on __deletedDatas collection for expireAt field
        /// </summary>
        private async Task EnsureTTLIndexAsync(IMongoDatabase database, string collectionName)
        {
            try
            {
                var collection = database.GetCollection<BsonDocument>(collectionName);
                var indexes = await collection.Indexes.ListAsync();
                var indexList = await indexes.ToListAsync();

                // Check if TTL index already exists
                var hasTTLIndex = indexList.Any(idx =>
                {
                    var keys = idx["key"].AsBsonDocument;
                    return keys.Contains("expireAt");
                });

                if (!hasTTLIndex)
                {
                    var indexKeys = Builders<BsonDocument>.IndexKeys.Ascending("expireAt");
                    var indexOptions = new CreateIndexOptions
                    {
                        ExpireAfter = TimeSpan.Zero, // MongoDB will use expireAt field value directly
                        Name = "idx_expireAt_ttl"
                    };

                    var indexModel = new CreateIndexModel<BsonDocument>(indexKeys, indexOptions);
                    await collection.Indexes.CreateOneAsync(indexModel);

                    _logger.LogInformation(
                        "Created TTL index on {CollectionName}.expireAt",
                        collectionName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to ensure TTL index on {CollectionName} (may already exist)",
                    collectionName);
            }
        }

        public async Task<IClientSessionHandle> StartSessionAsync()
        {
            return await _mongoClient.StartSessionAsync();
        }

        public async Task<List<BsonDocument>> AggregateAsync(
            string databaseName,
            string collectionName,
            List<BsonDocument> pipeline,
            IClientSessionHandle? session = null)
        {
            var database = _mongoClient.GetDatabase(databaseName);
            var collection = database.GetCollection<BsonDocument>(collectionName);

            PipelineDefinition<BsonDocument, BsonDocument> pipelineDefinition = pipeline;

            List<BsonDocument> results;
            if (session != null)
            {
                results = await collection.AggregateAsync(session, pipelineDefinition).Result.ToListAsync();
            }
            else
            {
                results = await collection.AggregateAsync(pipelineDefinition).Result.ToListAsync();
            }

            _logger.LogDebug(
                "Aggregate executed on {Database}.{Collection}, returned {Count} documents",
                databaseName, collectionName, results.Count);

            return results;
        }
    }
}

