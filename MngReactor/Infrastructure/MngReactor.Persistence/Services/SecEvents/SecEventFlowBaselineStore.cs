using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Persistence.Services.SecEvents;

/// <summary>
/// Mongo tabanlı src→dst akış baseline (U7). Demo: 1 benzersiz çift sonrası öğrenme tamamlanır.
/// </summary>
public sealed class SecEventFlowBaselineStore : ISecEventFlowBaselineStore
{
    internal const string CollectionName = "sec_flow_baseline";
    internal const string MetaDocumentId = "__meta__";

    /// <summary>Demo/pilot: tek benzersiz çift sonrası tespit fazına geç. Üretimde yükseltin veya admin ile tamamlayın.</summary>
    internal const int AutoCompleteAfterUniquePairs = 1;

    private static readonly ConcurrentDictionary<string, byte> IndexEnsuredDomains = new(StringComparer.OrdinalIgnoreCase);

    private readonly IMongoClient _mongoClient;
    private readonly ILogger<SecEventFlowBaselineStore> _logger;

    public SecEventFlowBaselineStore(IMongoClient mongoClient, ILogger<SecEventFlowBaselineStore> logger)
    {
        _mongoClient = mongoClient;
        _logger = logger;
    }

    public async Task<SecEventFlowBaselineApplyResult> ApplyFlowPairAsync(
        string domain,
        string srcIp,
        string dstIp,
        string originalEventAction,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return new SecEventFlowBaselineApplyResult(false);

        var normalizedSrc = srcIp.Trim();
        var normalizedDst = dstIp.Trim();
        if (string.IsNullOrEmpty(normalizedSrc) || string.IsNullOrEmpty(normalizedDst))
            return new SecEventFlowBaselineApplyResult(false);

        var databaseName = $"mng_{domain.Trim().ToLowerInvariant()}";
        var database = _mongoClient.GetDatabase(databaseName);
        await EnsureIndexesOnceAsync(database, databaseName, cancellationToken);
        var collection = database.GetCollection<BsonDocument>(CollectionName);

        var pairId = BuildPairId(normalizedSrc, normalizedDst);
        var meta = await GetOrCreateMetaAsync(collection, cancellationToken);

        if (meta.LearningComplete)
        {
            var exists = await collection.Find(Builders<BsonDocument>.Filter.Eq("_id", pairId))
                .Limit(1)
                .AnyAsync(cancellationToken);

            if (exists)
                return new SecEventFlowBaselineApplyResult(false);

            await UpsertPairAsync(collection, pairId, normalizedSrc, normalizedDst, cancellationToken);
            _logger.LogInformation(
                "sec_flow_baseline new_flow domain={Domain} src={Src} dst={Dst}",
                domain, normalizedSrc, normalizedDst);
            return new SecEventFlowBaselineApplyResult(true);
        }

        var insertedNewPair = await UpsertPairAsync(collection, pairId, normalizedSrc, normalizedDst, cancellationToken);
        if (insertedNewPair)
        {
            meta = await IncrementUniquePairCountAsync(collection, cancellationToken);
            if (meta.UniquePairCount >= AutoCompleteAfterUniquePairs)
                await SetLearningCompleteAsync(collection, cancellationToken);
        }

        return new SecEventFlowBaselineApplyResult(false);
    }

    private static string BuildPairId(string srcIp, string dstIp) => $"{srcIp}|{dstIp}";

    private static async Task<BaselineMeta> GetOrCreateMetaAsync(
        IMongoCollection<BsonDocument> collection,
        CancellationToken cancellationToken)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", MetaDocumentId);
        var existing = await collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
            return ReadMeta(existing);

        var doc = new BsonDocument
        {
            { "_id", MetaDocumentId },
            { "learningComplete", false },
            { "uniquePairCount", 0 },
            { "updatedAt", DateTime.UtcNow }
        };

        try
        {
            await collection.InsertOneAsync(doc, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            existing = await collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
            if (existing is not null)
                return ReadMeta(existing);
            throw;
        }

        return new BaselineMeta(false, 0);
    }

    private static BaselineMeta ReadMeta(BsonDocument doc) =>
        new(
            doc.GetValue("learningComplete", false).ToBoolean(),
            doc.GetValue("uniquePairCount", 0).ToInt32());

    private static async Task<bool> UpsertPairAsync(
        IMongoCollection<BsonDocument> collection,
        string pairId,
        string srcIp,
        string dstIp,
        CancellationToken cancellationToken)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", pairId);
        var existing = await collection.Find(filter).Limit(1).AnyAsync(cancellationToken);
        if (existing)
        {
            await collection.UpdateOneAsync(
                filter,
                Builders<BsonDocument>.Update.Set("lastSeenAt", DateTime.UtcNow),
                cancellationToken: cancellationToken);
            return false;
        }

        var doc = new BsonDocument
        {
            { "_id", pairId },
            { "srcIp", srcIp },
            { "dstIp", dstIp },
            { "firstSeenAt", DateTime.UtcNow },
            { "lastSeenAt", DateTime.UtcNow }
        };

        try
        {
            await collection.InsertOneAsync(doc, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    private static async Task<BaselineMeta> IncrementUniquePairCountAsync(
        IMongoCollection<BsonDocument> collection,
        CancellationToken cancellationToken)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", MetaDocumentId);
        var update = Builders<BsonDocument>.Update
            .Inc("uniquePairCount", 1)
            .Set("updatedAt", DateTime.UtcNow);
        var doc = await collection.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<BsonDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        return doc is null ? new BaselineMeta(false, 0) : ReadMeta(doc);
    }

    private static async Task SetLearningCompleteAsync(
        IMongoCollection<BsonDocument> collection,
        CancellationToken cancellationToken)
    {
        var filter = Builders<BsonDocument>.Filter.Eq("_id", MetaDocumentId);
        var update = Builders<BsonDocument>.Update
            .Set("learningComplete", true)
            .Set("updatedAt", DateTime.UtcNow);
        await collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
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
                Builders<BsonDocument>.IndexKeys.Ascending("srcIp").Ascending("dstIp"),
                new CreateIndexOptions { Name = "idx_src_dst" })
        };

        try
        {
            await collection.Indexes.CreateManyAsync(models, cancellationToken);
            _logger.LogInformation("sec_flow_baseline indeksleri olusturuldu: {Db}", databaseName);
        }
        catch (MongoCommandException ex) when (IsIndexConflict(ex))
        {
            _logger.LogDebug("sec_flow_baseline indeksleri zaten mevcut: {Db}", databaseName);
        }
        catch (Exception ex)
        {
            IndexEnsuredDomains.TryRemove(databaseName, out _);
            _logger.LogError(ex, "sec_flow_baseline indeks olusturma basarisiz: {Db}", databaseName);
            throw;
        }
    }

    private static bool IsIndexConflict(MongoCommandException ex) =>
        ex.CodeName is "IndexOptionsConflict" or "IndexKeySpecsConflict" || ex.Code == 85;

    private sealed record BaselineMeta(bool LearningComplete, int UniquePairCount);
}
