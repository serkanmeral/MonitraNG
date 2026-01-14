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

            // Filter: status = "Active"
            var filter = Builders<BsonDocument>.Filter.Eq("status", "Active");
            var domains = await collection.Find(filter).ToListAsync();

            var domainInfos = domains.Select(d => new DomainInfo
            {
                Id = d["_id"].AsObjectId.ToString(),
                Name = d.GetValue("name")?.AsString ?? string.Empty,
                DatabaseName = d.GetValue("databaseName")?.AsString ?? 
                               $"mng_{d.GetValue("name")?.AsString?.ToLowerInvariant() ?? ""}",
                Status = d.GetValue("status")?.AsString ?? "Unknown"
            }).ToList();

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

            var domainInfo = new DomainInfo
            {
                Id = domain["_id"].AsObjectId.ToString(),
                Name = domain.GetValue("name")?.AsString ?? string.Empty,
                DatabaseName = domain.GetValue("databaseName")?.AsString ?? 
                               $"mng_{domain.GetValue("name")?.AsString?.ToLowerInvariant() ?? ""}",
                Status = domain.GetValue("status")?.AsString ?? "Unknown"
            };

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
}
