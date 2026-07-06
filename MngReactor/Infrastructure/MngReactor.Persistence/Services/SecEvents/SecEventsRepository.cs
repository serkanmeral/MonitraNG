using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Configuration;
using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Persistence.Services.SecEvents;

public sealed class SecEventsRepository : ISecEventsRepository
{
    private const string CollectionName = "sec_events";

    private static readonly ConcurrentDictionary<string, byte> IndexEnsuredDomains = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, byte> TtlIndexEnsuredDomains = new(StringComparer.OrdinalIgnoreCase);

    private readonly IMongoClient _mongoClient;
    private readonly ILogger<SecEventsRepository> _logger;
    private readonly IMemoryCache _cache;
    private readonly int _hotTtlDays;
    private readonly int _dashboardSummaryCacheSeconds;
    private readonly bool _useDashboardHourlyRollup;
    private readonly SecEventHourlyRollupStore _rollupStore;

    public SecEventsRepository(
        IMongoClient mongoClient,
        IOptions<MngReactorSettings> options,
        IMemoryCache cache,
        ILogger<SecEventsRepository> logger)
    {
        _mongoClient = mongoClient;
        _cache = cache;
        _logger = logger;
        _hotTtlDays = options?.Value?.SecEvents?.HotTtlDays ?? 60;
        _dashboardSummaryCacheSeconds = options?.Value?.SecEvents?.DashboardSummaryCacheSeconds ?? 60;
        _useDashboardHourlyRollup = options?.Value?.SecEvents?.UseDashboardHourlyRollup ?? true;
        _rollupStore = new SecEventHourlyRollupStore(mongoClient);
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
        await EnsureTtlIndexOnceAsync(database, databaseName, cancellationToken);

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

        if (inserted > 0 && _useDashboardHourlyRollup)
        {
            try
            {
                await _rollupStore.IncrementFromDocumentsAsync(domain, docs, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "sec_events hourly rollup increment basarisiz, database={Db}", databaseName);
            }
        }

        _logger.LogDebug("sec_events: {Count} dokuman yazildi, database={Db}", inserted, databaseName);
        return inserted;
    }

    public async Task<SecEventQueryResult> QueryAsync(
        string domain,
        SecEventQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return new SecEventQueryResult { Items = Array.Empty<SecEventListItem>(), Total = 0 };

        var databaseName = $"mng_{domain.Trim().ToLowerInvariant()}";
        var database = _mongoClient.GetDatabase(databaseName);
        await EnsureIndexesOnceAsync(database, databaseName, cancellationToken);
        await EnsureTtlIndexOnceAsync(database, databaseName, cancellationToken);

        var collection = database.GetCollection<BsonDocument>(CollectionName);
        var mongoFilter = SecEventQueryFilterBuilder.Build(filter);
        var skip = SecEventQueryFilterBuilder.NormalizeSkip(filter.Skip);
        var limit = SecEventQueryFilterBuilder.NormalizeLimit(filter.Limit);

        var renderedFilter = mongoFilter.Render(
            new RenderArgs<BsonDocument>(
                BsonSerializer.SerializerRegistry.GetSerializer<BsonDocument>(),
                BsonSerializer.SerializerRegistry));

        var pipeline = new[]
        {
            new BsonDocument("$match", renderedFilter),
            new BsonDocument("$facet", new BsonDocument
            {
                {
                    "items", new BsonArray
                    {
                        new BsonDocument("$project", new BsonDocument("raw", 0)),
                        new BsonDocument("$sort", new BsonDocument("@timestamp", -1)),
                        new BsonDocument("$skip", skip),
                        new BsonDocument("$limit", limit),
                    }
                },
                {
                    "total", new BsonArray
                    {
                        new BsonDocument("$count", "n"),
                    }
                },
            }),
        };

        var facetRoot = await collection.Aggregate<BsonDocument>(pipeline)
            .FirstOrDefaultAsync(cancellationToken);

        var docs = facetRoot is not null
            && facetRoot.TryGetValue("items", out var itemsVal)
            && itemsVal.IsBsonArray
            ? itemsVal.AsBsonArray.Where(x => x.IsBsonDocument).Select(x => x.AsBsonDocument).ToList()
            : [];

        long total = 0;
        if (facetRoot is not null
            && facetRoot.TryGetValue("total", out var totalVal)
            && totalVal.IsBsonArray
            && totalVal.AsBsonArray.Count > 0
            && totalVal.AsBsonArray[0].IsBsonDocument)
        {
            total = totalVal.AsBsonArray[0].AsBsonDocument.GetValue("n", 0).ToInt64();
        }

        var items = docs.Select(d => SecEventBsonReader.ToListItem(d)).ToList();
        return new SecEventQueryResult { Items = items, Total = total };
    }

    public async Task<SecEventListItem?> GetByIdAsync(
        string domain,
        string id,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(id))
            return null;

        if (!ObjectId.TryParse(id, out var objectId))
            return null;

        var databaseName = $"mng_{domain.Trim().ToLowerInvariant()}";
        var collection = _mongoClient.GetDatabase(databaseName).GetCollection<BsonDocument>(CollectionName);
        var doc = await collection
            .Find(Builders<BsonDocument>.Filter.Eq("_id", objectId))
            .FirstOrDefaultAsync(cancellationToken);

        return doc is null ? null : SecEventBsonReader.ToListItem(doc, includeRaw: true);
    }

    public async Task<SecEventDashboardSummary> GetDashboardSummaryAsync(
        string domain,
        SecEventDashboardSummaryRequest request,
        CancellationToken cancellationToken = default)
    {
        var rangeHours = Math.Clamp(request?.RangeHours ?? 24, 1, 168);
        var excludeUnknown = request?.ExcludeUnknown ?? true;

        if (string.IsNullOrWhiteSpace(domain) || _dashboardSummaryCacheSeconds <= 0)
            return await LoadDashboardSummaryCoreAsync(domain, rangeHours, excludeUnknown, cancellationToken);

        var cacheKey = BuildDashboardSummaryCacheKey(domain, rangeHours, excludeUnknown);
        if (_cache.TryGetValue(cacheKey, out SecEventDashboardSummary? cached) && cached is not null)
            return cached;

        var summary = await LoadDashboardSummaryCoreAsync(domain, rangeHours, excludeUnknown, cancellationToken);
        _cache.Set(cacheKey, summary, TimeSpan.FromSeconds(_dashboardSummaryCacheSeconds));
        return summary;
    }

    private static string BuildDashboardSummaryCacheKey(string domain, int rangeHours, bool excludeUnknown)
    {
        var normalizedDomain = domain.Trim().ToLowerInvariant();
        return $"sec_events:dashboard:{normalizedDomain}:{rangeHours}:{excludeUnknown}";
    }

    private async Task<SecEventDashboardSummary> LoadDashboardSummaryCoreAsync(
        string domain,
        int rangeHours,
        bool excludeUnknown,
        CancellationToken cancellationToken)
    {
        var (from, to, hourStarts) = SecEventDashboardAggregator.BuildWindow(rangeHours);

        if (string.IsNullOrWhiteSpace(domain))
        {
            return new SecEventDashboardSummary
            {
                Range = $"{rangeHours}h",
                From = from,
                To = to,
                EventsTotal = 0,
                ByAction = new Dictionary<string, long>(),
                Hourly = hourStarts.Select(h => new SecEventHourlyBucket { HourStart = h, Count = 0 }).ToList(),
            };
        }

        var databaseName = $"mng_{domain.Trim().ToLowerInvariant()}";
        var database = _mongoClient.GetDatabase(databaseName);
        await EnsureIndexesOnceAsync(database, databaseName, cancellationToken);
        await EnsureTtlIndexOnceAsync(database, databaseName, cancellationToken);

        if (_useDashboardHourlyRollup)
        {
            var rollupSummary = await _rollupStore.TryBuildSummaryAsync(
                domain, rangeHours, excludeUnknown, cancellationToken);
            if (rollupSummary is not null)
            {
                _logger.LogDebug(
                    "dashboard-summary rollup hit, domain={Domain}, rangeHours={Range}",
                    domain,
                    rangeHours);
                return rollupSummary;
            }
        }

        var collection = database.GetCollection<BsonDocument>(CollectionName);
        var pipeline = SecEventDashboardAggregator.BuildPipeline(from, to, excludeUnknown);
        var cursor = await collection.AggregateAsync<BsonDocument>(pipeline, cancellationToken: cancellationToken);
        var docs = await cursor.ToListAsync(cancellationToken);
        var facetRoot = docs.FirstOrDefault() ?? new BsonDocument();

        return SecEventDashboardAggregator.ParseResult(facetRoot, rangeHours, from, to, hourStarts);
    }

    private async Task EnsureIndexesOnceAsync(
        IMongoDatabase database,
        string databaseName,
        CancellationToken cancellationToken)
    {
        if (!IndexEnsuredDomains.TryAdd(databaseName, 0))
            return;

        var collection = database.GetCollection<BsonDocument>(CollectionName);
        var models = new List<CreateIndexModel<BsonDocument>>
        {
            new(
                Builders<BsonDocument>.IndexKeys.Descending("@timestamp"),
                new CreateIndexOptions { Name = "idx_timestamp_desc" }),
            new(
                Builders<BsonDocument>.IndexKeys.Ascending("source.type").Descending("@timestamp"),
                new CreateIndexOptions { Name = "idx_sourceType_timestamp" }),
            new(
                Builders<BsonDocument>.IndexKeys.Ascending("event.action").Descending("@timestamp"),
                new CreateIndexOptions { Name = "idx_eventAction_timestamp" }),
            new(
                Builders<BsonDocument>.IndexKeys.Ascending("network.srcIp").Descending("@timestamp"),
                new CreateIndexOptions { Name = "idx_networkSrcIp_timestamp" }),
            new(
                Builders<BsonDocument>.IndexKeys.Descending(SecEventDashboardAggregator.DashboardTimeField),
                new CreateIndexOptions { Name = "idx_ingestedAt_desc" }),
            new(
                Builders<BsonDocument>.IndexKeys
                    .Descending(SecEventDashboardAggregator.DashboardTimeField)
                    .Ascending("event.action"),
                new CreateIndexOptions { Name = "idx_ingestedAt_eventAction" }),
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

    private async Task EnsureTtlIndexOnceAsync(
        IMongoDatabase database,
        string databaseName,
        CancellationToken cancellationToken)
    {
        if (_hotTtlDays <= 0)
            return;

        if (!TtlIndexEnsuredDomains.TryAdd(databaseName, 0))
            return;

        var collection = database.GetCollection<BsonDocument>(CollectionName);
        var model = new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("@timestamp"),
            new CreateIndexOptions
            {
                Name = "idx_timestamp_ttl",
                ExpireAfter = TimeSpan.FromDays(_hotTtlDays)
            });

        try
        {
            await collection.Indexes.CreateOneAsync(model, new CreateOneIndexOptions(), cancellationToken);
            _logger.LogInformation(
                "sec_events TTL indeksi olusturuldu: {Db}.{Coll}, hotTtlDays={Ttl}",
                databaseName,
                CollectionName,
                _hotTtlDays);
        }
        catch (MongoCommandException ex) when (IsIndexConflict(ex))
        {
            _logger.LogDebug("sec_events TTL indeksi zaten mevcut: {Db}.{Coll}", databaseName, CollectionName);
        }
        catch (Exception ex)
        {
            TtlIndexEnsuredDomains.TryRemove(databaseName, out _);
            _logger.LogError(ex, "sec_events TTL indeks olusturma basarisiz: {Db}", databaseName);
            throw;
        }
    }

    private static bool IsIndexConflict(MongoCommandException ex) =>
        ex.CodeName is "IndexOptionsConflict" or "IndexKeySpecsConflict" || ex.Code == 85;
}
