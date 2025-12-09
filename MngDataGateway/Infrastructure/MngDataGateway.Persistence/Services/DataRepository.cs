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

            var filterBuilder = Builders<BsonDocument>.Filter;
            var filter = filterBuilder.Eq("__dataId", dataId);

            // Exclude soft-deleted unless requested
            if (!includeDeleted)
            {
                filter = filterBuilder.And(
                    filter,
                    filterBuilder.Or(
                        filterBuilder.Eq("__isDeleted", false),
                        filterBuilder.Exists("__isDeleted", false)
                    )
                );
            }

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

            var filterBuilder = Builders<BsonDocument>.Filter;
            FilterDefinition<BsonDocument> filter = filterBuilder.Empty;

            // Exclude soft-deleted unless requested
            if (!includeDeleted)
            {
                filter = filterBuilder.Or(
                    filterBuilder.Eq("__isDeleted", false),
                    filterBuilder.Exists("__isDeleted", false)
                );
            }

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

            var filterBuilder = Builders<BsonDocument>.Filter;
            var filter = filterBuilder.And(
                filterBuilder.Eq("__dataId", dataId),
                filterBuilder.Or(
                    filterBuilder.Eq("__isDeleted", false),
                    filterBuilder.Exists("__isDeleted", false)
                )
            );

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

        public async Task<bool> SoftDeleteAsync(
            string databaseName,
            string collectionName,
            string dataId,
            string userId,
            string userEmail,
            string? ipAddress = null)
        {
            var database = _mongoClient.GetDatabase(databaseName);
            var collection = database.GetCollection<BsonDocument>(collectionName);

            var filterBuilder = Builders<BsonDocument>.Filter;
            var filter = filterBuilder.And(
                filterBuilder.Eq("__dataId", dataId),
                filterBuilder.Or(
                    filterBuilder.Eq("__isDeleted", false),
                    filterBuilder.Exists("__isDeleted", false)
                )
            );

            var deleteInfo = new BsonDocument
            {
                { "userId", userId },
                { "userEmail", userEmail },
                { "timestamp", DateTime.UtcNow },
                { "ipAddress", string.IsNullOrEmpty(ipAddress) ? BsonNull.Value : (BsonValue)ipAddress }
            };

            var update = Builders<BsonDocument>.Update
                .Set("__isDeleted", true)
                .Set("__deleteInfo", deleteInfo);

            var result = await collection.UpdateOneAsync(filter, update);

            _logger.LogInformation(
                "Soft-deleted document __dataId: {DataId} in {Database}.{Collection} by user {UserId}",
                dataId, databaseName, collectionName, userId);

            return result.MatchedCount > 0;
        }

        public async Task<bool> RestoreAsync(
            string databaseName,
            string collectionName,
            string dataId,
            string userId,
            string userEmail,
            string? ipAddress = null)
        {
            var database = _mongoClient.GetDatabase(databaseName);
            var collection = database.GetCollection<BsonDocument>(collectionName);

            var filterBuilder = Builders<BsonDocument>.Filter;
            var filter = filterBuilder.And(
                filterBuilder.Eq("__dataId", dataId),
                filterBuilder.Eq("__isDeleted", true)
            );

            var restoreInfo = new BsonDocument
            {
                { "userId", userId },
                { "userEmail", userEmail },
                { "timestamp", DateTime.UtcNow },
                { "ipAddress", string.IsNullOrEmpty(ipAddress) ? BsonNull.Value : (BsonValue)ipAddress }
            };

            var update = Builders<BsonDocument>.Update
                .Set("__isDeleted", false)
                .Set("__restoreInfo", restoreInfo);

            var result = await collection.UpdateOneAsync(filter, update);

            _logger.LogInformation(
                "Restored document __dataId: {DataId} in {Database}.{Collection} by user {UserId}",
                dataId, databaseName, collectionName, userId);

            return result.MatchedCount > 0;
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

