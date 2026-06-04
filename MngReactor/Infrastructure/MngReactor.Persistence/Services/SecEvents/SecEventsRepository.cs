using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Persistence.Services.SecEvents;

public sealed class SecEventsRepository : ISecEventsRepository
{
    private const string CollectionName = "sec_events";

    private static readonly ConcurrentDictionary<string, byte> IndexEnsuredDomains = new(StringComparer.OrdinalIgnoreCase);

    private readonly IMongoClient _mongoClient;
    private readonly ILogger<SecEventsRepository> _logger;

    public SecEventsRepository(IMongoClient mongoClient, ILogger<SecEventsRepository> logger)
    {
        _mongoClient = mongoClient;
        _logger = logger;
    }

    public async Task<int> InsertManyAsync(
        string domain,
        IReadOnlyList<SecEventDocument> docs,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            _logger.LogWarning("sec_events InsertMany: domain bos, yazim atlandi");
            return 0;
        }

        if (docs.Count == 0)
            return 0;

        var databaseName = $"mng_{domain.Trim().ToLowerInvariant()}";
        var database = _mongoClient.GetDatabase(databaseName);
        await EnsureIndexesOnceAsync(database, databaseName, cancellationToken);

        var collection = database.GetCollection<BsonDocument>(CollectionName);
        var inserted = 0;

        foreach (var chunk in SecEventBatchChunker.Chunk(docs, SecEventIngestLimits.MongoBulkChunkSize))
        {
            var bsonDocs = new List<BsonDocument>(chunk.Count);
            foreach (var doc in chunk)
                bsonDocs.Add(SecEventDocumentBsonMapper.ToBsonDocument(doc));

            if (bsonDocs.Count == 0)
                continue;

            try
            {
                await collection.InsertManyAsync(bsonDocs, cancellationToken: cancellationToken);
                inserted += bsonDocs.Count;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "sec_events InsertMany basarisiz, database={Db}, chunkSize={Size}",
                    databaseName, bsonDocs.Count);
                throw;
            }
        }

        _logger.LogDebug("sec_events: {Count} dokuman yazildi, database={Db}", inserted, databaseName);
        return inserted;
    }

    private async Task EnsureIndexesOnceAsync(
        IMongoDatabase database,
        string databaseName,
        CancellationToken cancellationToken)
    {
        if (!IndexEnsuredDomains.TryAdd(databaseName, 0))
            return;

        var collection = database.GetCollection<BsonDocument>(CollectionName);
        var models = new[]
        {
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Descending("@timestamp"),
                new CreateIndexOptions { Name = "idx_timestamp_desc" }),
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("source.type").Descending("@timestamp"),
                new CreateIndexOptions { Name = "idx_sourceType_timestamp" }),
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("event.action").Descending("@timestamp"),
                new CreateIndexOptions { Name = "idx_eventAction_timestamp" }),
            new CreateIndexModel<BsonDocument>(
                Builders<BsonDocument>.IndexKeys.Ascending("network.srcIp").Descending("@timestamp"),
                new CreateIndexOptions { Name = "idx_networkSrcIp_timestamp" })
        };

        try
        {
            await collection.Indexes.CreateManyAsync(models, cancellationToken);
            _logger.LogInformation("sec_events indeksleri olusturuldu: {Db}.{Coll}", databaseName, CollectionName);
        }
        catch (MongoCommandException ex) when (IsIndexConflict(ex))
        {
            _logger.LogDebug("sec_events indeksleri zaten mevcut: {Db}.{Coll}", databaseName, CollectionName);
        }
        catch (Exception ex)
        {
            IndexEnsuredDomains.TryRemove(databaseName, out _);
            _logger.LogError(ex, "sec_events indeks olusturma basarisiz: {Db}", databaseName);
            throw;
        }
    }

    private static bool IsIndexConflict(MongoCommandException ex) =>
        ex.CodeName is "IndexOptionsConflict" or "IndexKeySpecsConflict" || ex.Code == 85;
}
