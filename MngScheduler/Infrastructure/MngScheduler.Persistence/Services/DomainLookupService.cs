using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using MngScheduler.Application.Configuration;
using MngScheduler.Application.Interfaces;

namespace MngScheduler.Persistence.Services;

/// <summary>
/// Domain lookup service - MngKeeper MongoDB'den domain bilgilerini çeker ve cache'ler
/// </summary>
public class DomainLookupService : IDomainLookupService
{
    private readonly ILogger<DomainLookupService> _logger;
    private readonly IMongoClient _mongoClient;
    private readonly IMemoryCache _cache;
    private readonly MngSchedulerSettings _settings;
    private const string CacheKeyPrefix = "domain_lookup_";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(30);

    public DomainLookupService(
        ILogger<DomainLookupService> logger,
        IMongoClient mongoClient,
        IMemoryCache cache,
        IOptions<MngSchedulerSettings> settings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mongoClient = mongoClient ?? throw new ArgumentNullException(nameof(mongoClient));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
    }

    public async Task<IEnumerable<DomainInfo>> GetActiveDomainsAsync()
    {
        try
        {
            var cacheKey = $"{CacheKeyPrefix}active_domains";
            if (_cache.TryGetValue(cacheKey, out IEnumerable<DomainInfo>? cachedDomains) && cachedDomains != null)
            {
                _logger.LogDebug("Active domains found in cache: {Count}", cachedDomains.Count());
                return cachedDomains;
            }

            var databaseName = _settings.MongoDB.KeeperDatabaseName ?? "mngkeeper";
            var database = _mongoClient.GetDatabase(databaseName);
            var collection = database.GetCollection<BsonDocument>("domains");

            // Tanılama: toplam domain sayısı ve kaçı Active
            var totalDomains = await collection.CountDocumentsAsync(new BsonDocument());
            // Keeper Mongo: status string "Active" veya enum int 1 (DomainStatus.Active)
            var activeFilter = BuildActiveDomainFilter();
            var activeCount = await collection.CountDocumentsAsync(activeFilter);
            _logger.LogInformation(
                "[DirectorySync] Domain lookup DB={DatabaseName} collection=domains total={Total} active={ActiveCount}",
                databaseName, totalDomains, activeCount);

            if (totalDomains > 0 && activeCount == 0)
            {
                var sample = await collection.Find(FilterDefinition<BsonDocument>.Empty).Limit(1)
                    .FirstOrDefaultAsync();
                if (sample != null && sample.Contains("status"))
                {
                    _logger.LogWarning(
                        "[DirectorySync] Domain var ama Active filtresine uymuyor. name={Name} statusRaw={StatusRaw} — Mongo'da status=1 veya \"Active\" olmalı",
                        sample.GetValue("name")?.ToString() ?? sample.GetValue("realmName")?.ToString() ?? "?",
                        sample["status"]);
                }
            }

            var domains = await collection.Find(activeFilter).ToListAsync();

            var domainInfos = domains.Select(MapDomainDocument).ToList();

            // Cache the result
            _cache.Set(cacheKey, domainInfos, CacheExpiration);
            _logger.LogDebug("Active domains cached: {Count}", domainInfos.Count);

            return domainInfos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active domains");
            return Enumerable.Empty<DomainInfo>();
        }
    }

    public async Task<DomainInfo?> GetDomainByIdAsync(string domainId)
    {
        if (string.IsNullOrWhiteSpace(domainId))
            return null;

        try
        {
            var cacheKey = $"{CacheKeyPrefix}domain_{domainId}";
            if (_cache.TryGetValue(cacheKey, out DomainInfo? cachedDomain) && cachedDomain != null)
            {
                _logger.LogDebug("Domain found in cache: DomainId={DomainId}", domainId);
                return cachedDomain;
            }

            var databaseName = _settings.MongoDB.KeeperDatabaseName ?? "mngkeeper";
            var database = _mongoClient.GetDatabase(databaseName);
            var collection = database.GetCollection<BsonDocument>("domains");

            var filter = Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(domainId));
            var domain = await collection.Find(filter).FirstOrDefaultAsync();

            if (domain == null)
            {
                _logger.LogWarning("Domain not found: DomainId={DomainId}", domainId);
                return null;
            }

            var domainInfo = MapDomainDocument(domain);

            // Cache the result
            _cache.Set(cacheKey, domainInfo, CacheExpiration);
            _logger.LogDebug("Domain cached: DomainId={DomainId}", domainId);

            return domainInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting domain by ID: DomainId={DomainId}", domainId);
            return null;
        }
    }

    public async Task<string?> GetDatabaseNameAsync(string domainId)
    {
        var domain = await GetDomainByIdAsync(domainId);
        return domain?.DatabaseName;
    }

    /// <summary>
    /// MngKeeper <see cref="MngKeeper.Domain.Entities.DomainStatus"/>: Active = 1 (int) veya "Active" (string).
    /// </summary>
    private static FilterDefinition<BsonDocument> BuildActiveDomainFilter() =>
        Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Eq("status", "Active"),
            Builders<BsonDocument>.Filter.Eq("status", 1));

    private static DomainInfo MapDomainDocument(BsonDocument d)
    {
        var name = d.GetValue("name", BsonNull.Value).IsBsonNull
            ? string.Empty
            : d["name"].AsString;
        if (string.IsNullOrWhiteSpace(name) && d.Contains("realmName") && !d["realmName"].IsBsonNull)
            name = d["realmName"].AsString;

        return new DomainInfo
        {
            Id = d["_id"].AsObjectId.ToString(),
            Name = name,
            DatabaseName = d.GetValue("databaseName", BsonNull.Value).IsBsonNull
                ? $"mng_{name.ToLowerInvariant()}"
                : d["databaseName"].AsString,
            Status = ReadStatusLabel(d)
        };
    }

    private static string ReadStatusLabel(BsonDocument d)
    {
        if (!d.Contains("status") || d["status"].IsBsonNull)
            return "Unknown";

        return d["status"].BsonType switch
        {
            BsonType.String => d["status"].AsString,
            BsonType.Int32 => d["status"].AsInt32 switch
            {
                1 => "Active",
                0 => "Pending",
                2 => "Suspended",
                3 => "Expired",
                4 => "Deleted",
                5 => "Failed",
                _ => d["status"].AsInt32.ToString()
            },
            _ => d["status"].ToString() ?? "Unknown"
        };
    }
}
