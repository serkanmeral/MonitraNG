using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MngReactor.Application.Abstractions.Ingest;
using MngReactor.Application.Configuration;

namespace MngReactor.Persistence.Services.Ingest;

public class MonMetricsRepository : IMonMetricsRepository
{
    private const string CollectionName = "mon_metrics";

    private readonly IMongoClient _mongoClient;
    private readonly ILogger<MonMetricsRepository> _logger;
    private readonly int _ttlDays;

    public MonMetricsRepository(
        IMongoClient mongoClient,
        IOptions<MngReactorSettings> options,
        ILogger<MonMetricsRepository> logger)
    {
        _mongoClient = mongoClient;
        _logger = logger;
        _ttlDays = options?.Value?.Monitoring?.MetricsTtlDays ?? 90;
    }

    public async Task<int> InsertManyAsync(string domain, IReadOnlyList<JsonObject> documents, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            _logger.LogWarning("MonMetrics InsertMany: domain bos, yazim atlandi");
            return 0;
        }

        if (documents == null || documents.Count == 0)
            return 0;

        var databaseName = $"mng_{domain.Trim().ToLowerInvariant()}";
        var database = _mongoClient.GetDatabase(databaseName);

        await EnsureTimeSeriesCollectionAsync(database, ct);
        await EnsureMetricsIndexAsync(database, ct);

        var collection = database.GetCollection<BsonDocument>(CollectionName);
        var bsonDocs = documents.Select(JsonObjectToBsonDocument).Where(d => d != null).Cast<BsonDocument>().ToList();

        if (bsonDocs.Count == 0)
            return 0;

        try
        {
            await collection.InsertManyAsync(bsonDocs, cancellationToken: ct);
            _logger.LogDebug("MonMetrics: {Count} dokuman yazildi, database={Db}", bsonDocs.Count, databaseName);
            return bsonDocs.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MonMetrics InsertMany basarisiz, database={Db}", databaseName);
            throw;
        }
    }

    private async Task EnsureTimeSeriesCollectionAsync(IMongoDatabase database, CancellationToken ct)
    {
        var collections = await database.ListCollectionNamesAsync(cancellationToken: ct);
        var names = await collections.ToListAsync(ct);
        if (names.Contains(CollectionName))
            return;

        var expireAfterSeconds = _ttlDays > 0 ? _ttlDays * 86400 : 0;
        var options = new CreateCollectionOptions
        {
            TimeSeriesOptions = new TimeSeriesOptions("timestamp", metaField: "meta", granularity: TimeSeriesGranularity.Seconds)
        };
        if (expireAfterSeconds > 0)
            options.ExpireAfter = TimeSpan.FromSeconds(expireAfterSeconds);

        try
        {
            await database.CreateCollectionAsync(CollectionName, options, ct);
            _logger.LogInformation("MonMetrics Time Series koleksiyonu olusturuldu: {Db}.{Coll}, TTL={Ttl}gun",
                database.DatabaseNamespace.DatabaseName, CollectionName, _ttlDays);
        }
        catch (MongoCommandException ex) when (ex.Code == 48 || ex.CodeName == "NamespaceExists")
        {
            _logger.LogDebug("MonMetrics koleksiyonu zaten mevcut: {Db}.{Coll}", database.DatabaseNamespace.DatabaseName, CollectionName);
        }
    }

    /// <summary>
    /// DG/UI sorgulari icin compound index: meta.assetId + meta.collectibleCode + timestamp (desc).
    /// Sorgu: { "$and": [{ "meta.assetId": "..." }, { "meta.collectibleCode": "..." }] }, sort: -timestamp
    /// </summary>
    private async Task EnsureMetricsIndexAsync(IMongoDatabase database, CancellationToken ct)
    {
        var collection = database.GetCollection<BsonDocument>(CollectionName);
        var indexKeys = Builders<BsonDocument>.IndexKeys
            .Ascending("meta.assetId")
            .Ascending("meta.collectibleCode")
            .Descending("timestamp");

        var options = new CreateIndexOptions { Name = "idx_assetId_collectibleCode_timestamp" };

        try
        {
            await collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(indexKeys, options), cancellationToken: ct);
            _logger.LogDebug("MonMetrics index olusturuldu/kontrol edildi: {Db}.{Coll}", database.DatabaseNamespace.DatabaseName, CollectionName);
        }
        catch (MongoCommandException ex) when (ex.CodeName == "IndexOptionsConflict" || ex.CodeName == "IndexKeySpecsConflict" || ex.Code == 85)
        {
            _logger.LogDebug("MonMetrics index zaten mevcut: {Db}.{Coll}", database.DatabaseNamespace.DatabaseName, CollectionName);
        }
    }

    private static BsonDocument? JsonObjectToBsonDocument(JsonObject? obj)
    {
        if (obj == null) return null;
        try
        {
            var json = obj.ToJsonString();
            var doc = BsonDocument.Parse(json);
            // Time Series requires timestamp as BSON UTC DateTime - JSON serialization produces string
            EnsureTimestampAsBsonDateTime(doc);
            return doc;
        }
        catch
        {
            return null;
        }
    }

    private static void EnsureTimestampAsBsonDateTime(BsonDocument doc)
    {
        if (!doc.Contains("timestamp")) return;
        var ts = doc["timestamp"];
        if (ts is BsonDateTime) return; // already correct
        DateTime dt;
        if (ts is BsonString tsStr && DateTime.TryParse(tsStr.Value, null, System.Globalization.DateTimeStyles.RoundtripKind, out dt))
        {
            doc["timestamp"] = new BsonDateTime(dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime());
        }
        else if (ts is BsonInt64 l)
        {
            dt = DateTimeOffset.FromUnixTimeMilliseconds(l.Value).UtcDateTime;
            doc["timestamp"] = new BsonDateTime(dt);
        }
        else if (ts is BsonDouble d)
        {
            dt = DateTimeOffset.FromUnixTimeMilliseconds((long)d.Value).UtcDateTime;
            doc["timestamp"] = new BsonDateTime(dt);
        }
    }
}
