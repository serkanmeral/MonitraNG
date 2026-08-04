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
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISecEventOpenSearchWriter _openSearchWriter;
    private readonly SecEventsSettings _secEventsSettings;
    private readonly int _hotTtlDays;
    private readonly int _dashboardSummaryCacheSeconds;
    private readonly bool _useDashboardHourlyRollup;
    private readonly SecEventHourlyRollupStore _rollupStore;

    public SecEventsRepository(
        IMongoClient mongoClient,
        IOptions<MngReactorSettings> options,
        IMemoryCache cache,
        IHttpClientFactory httpClientFactory,
        ISecEventOpenSearchWriter openSearchWriter,
        ILogger<SecEventsRepository> logger)
    {
        _mongoClient = mongoClient;
        _cache = cache;
        _httpClientFactory = httpClientFactory;
        _openSearchWriter = openSearchWriter;
        _logger = logger;
        _secEventsSettings = options?.Value?.SecEvents ?? new SecEventsSettings();
        _hotTtlDays = _secEventsSettings.HotTtlDays;
        _dashboardSummaryCacheSeconds = _secEventsSettings.DashboardSummaryCacheSeconds;
        _useDashboardHourlyRollup = _secEventsSettings.UseDashboardHourlyRollup;
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
            var osItems = new List<SecEventOpenSearchIndexItem>(chunk.Count);
            foreach (var doc in chunk)
            {
                var id = ObjectId.GenerateNewId();
                var bson = SecEventDocumentBsonMapper.ToBsonDocument(doc);
                bson["_id"] = id;
                bsonDocs.Add(bson);
                osItems.Add(new SecEventOpenSearchIndexItem(id.ToString(), doc));
            }

            if (bsonDocs.Count == 0)
                continue;

            try
            {
                await collection.InsertManyAsync(bsonDocs, cancellationToken: cancellationToken);
                inserted += bsonDocs.Count;

                if (_secEventsSettings.OpenSearchDualWriteEnabled)
                {
                    var pairs = osItems.Select(i => (i.Id, i.Document)).ToList();
                    _ = _openSearchWriter.IndexManyAsync(domain, pairs, cancellationToken);
                }
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

        if (_secEventsSettings.OpenSearchReadEnabled)
        {
            var reader = new SecEventOpenSearchReader(_httpClientFactory, _logger, _secEventsSettings);
            return await reader.QueryAsync(domain, filter, cancellationToken);
        }

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

        if (_secEventsSettings.OpenSearchReadEnabled)
        {
            var reader = new SecEventOpenSearchReader(_httpClientFactory, _logger, _secEventsSettings);
            return await reader.GetByIdAsync(domain, id, cancellationToken);
        }

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

    public async Task<SecEventScopeOptions> GetScopeOptionsAsync(
        string domain,
        int rangeHours = 168,
        CancellationToken cancellationToken = default)
    {
        var hours = Math.Clamp(rangeHours, 1, 720);
        if (string.IsNullOrWhiteSpace(domain))
        {
            return new SecEventScopeOptions
            {
                Types = [],
                Products = [],
                Hosts = [],
                RangeHours = hours,
                Source = "empty"
            };
        }

        if (_secEventsSettings.OpenSearchReadEnabled)
        {
            var reader = new SecEventOpenSearchReader(_httpClientFactory, _logger, _secEventsSettings);
            return await reader.GetScopeOptionsAsync(domain, hours, cancellationToken);
        }

        return await GetScopeOptionsFromMongoAsync(domain, hours, cancellationToken);
    }

    private async Task<SecEventScopeOptions> GetScopeOptionsFromMongoAsync(
        string domain,
        int rangeHours,
        CancellationToken cancellationToken)
    {
        var databaseName = $"mng_{domain.Trim().ToLowerInvariant()}";
        var database = _mongoClient.GetDatabase(databaseName);
        await EnsureIndexesOnceAsync(database, databaseName, cancellationToken);
        var collection = database.GetCollection<BsonDocument>(CollectionName);

        var from = DateTime.UtcNow.AddHours(-rangeHours);
        var match = Builders<BsonDocument>.Filter.Gte(
            SecEventDashboardAggregator.DashboardTimeField,
            new BsonDateTime(from));

        var types = await DistinctNonEmptyAsync(collection, "source.type", match, cancellationToken);
        var products = await DistinctNonEmptyAsync(collection, "source.product", match, cancellationToken);
        var hosts = await DistinctNonEmptyAsync(collection, "source.host", match, cancellationToken);

        return new SecEventScopeOptions
        {
            Types = types,
            Products = products,
            Hosts = hosts,
            RangeHours = rangeHours,
            Source = "mongo"
        };
    }

    private static async Task<IReadOnlyList<string>> DistinctNonEmptyAsync(
        IMongoCollection<BsonDocument> collection,
        string field,
        FilterDefinition<BsonDocument> match,
        CancellationToken cancellationToken)
    {
        var values = await collection.Distinct<string>(field, match).ToListAsync(cancellationToken);
        return values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .Take(200)
            .ToList();
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

        if (_secEventsSettings.OpenSearchReadEnabled)
        {
            var reader = new SecEventOpenSearchReader(_httpClientFactory, _logger, _secEventsSettings);
            return await reader.GetDashboardSummaryAsync(
                domain, rangeHours, excludeUnknown, from, to, hourStarts, cancellationToken);
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
