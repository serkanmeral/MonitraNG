using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Driver;
using MongoDB.Bson;
using MngDataGateway.Application.Configuration;
using MngDataGateway.Application.Services;
using Microsoft.Extensions.Options;

namespace MngDataGateway.Persistence.Services
{
    /// <summary>
    /// Domain lookup service - MngKeeper MongoDB'den domain bilgilerini çeker ve cache'ler
    /// </summary>
    public class DomainLookupService : IDomainLookupService
    {
        private readonly ILogger<DomainLookupService> _logger;
        private readonly IMongoClient _mongoClient;
        private readonly IMemoryCache _cache;
        private readonly MngDataGatewaySettings _settings;
        private const string CacheKeyPrefix = "domain_lookup_";
        private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(30);

        public DomainLookupService(
            ILogger<DomainLookupService> logger,
            IMongoClient mongoClient,
            IMemoryCache cache,
            IOptions<MngDataGatewaySettings> settings)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mongoClient = mongoClient ?? throw new ArgumentNullException(nameof(mongoClient));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        }

        public async Task<string?> GetDomainNameAsync(string domainId)
        {
            if (string.IsNullOrWhiteSpace(domainId))
                return null;

            // Check cache first
            var cacheKey = $"{CacheKeyPrefix}name_{domainId}";
            if (_cache.TryGetValue(cacheKey, out string? cachedName) && !string.IsNullOrEmpty(cachedName))
            {
                _logger.LogDebug("Domain name found in cache: DomainId={DomainId}, Name={Name}", domainId, cachedName);
                return cachedName;
            }

            try
            {
                // Get from MngKeeper MongoDB
                var mngKeeperDbName = _settings.MongoDB.MngKeeperDatabaseName ?? "mngkeeper";
                var database = _mongoClient.GetDatabase(mngKeeperDbName);
                var collection = database.GetCollection<BsonDocument>("domains");

                var filter = Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(domainId));
                var domain = await collection.Find(filter).FirstOrDefaultAsync();

                if (domain == null)
                {
                    _logger.LogWarning("Domain not found: DomainId={DomainId}", domainId);
                    return null;
                }

                var domainName = domain.GetValue("name")?.AsString;
                if (string.IsNullOrEmpty(domainName))
                {
                    _logger.LogWarning("Domain name is empty: DomainId={DomainId}", domainId);
                    return null;
                }

                // Cache the result
                _cache.Set(cacheKey, domainName, CacheExpiration);
                _logger.LogDebug("Domain name cached: DomainId={DomainId}, Name={Name}", domainId, domainName);

                return domainName;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting domain name: DomainId={DomainId}", domainId);
                return null;
            }
        }

        public async Task<string?> GetDatabaseNameAsync(string domainId)
        {
            if (string.IsNullOrWhiteSpace(domainId))
                return null;

            // Check cache first
            var cacheKey = $"{CacheKeyPrefix}db_{domainId}";
            if (_cache.TryGetValue(cacheKey, out string? cachedDbName) && !string.IsNullOrEmpty(cachedDbName))
            {
                _logger.LogDebug("Database name found in cache: DomainId={DomainId}, Database={Database}", domainId, cachedDbName);
                return cachedDbName;
            }

            try
            {
                // Get from MngKeeper MongoDB
                var database = _mongoClient.GetDatabase("mngkeeper");
                var collection = database.GetCollection<BsonDocument>("domains");

                var filter = Builders<BsonDocument>.Filter.Eq("_id", ObjectId.Parse(domainId));
                var domain = await collection.Find(filter).FirstOrDefaultAsync();

                if (domain == null)
                {
                    _logger.LogWarning("Domain not found: DomainId={DomainId}", domainId);
                    return null;
                }

                // Try databaseName field first, if not exists, construct from name
                string? databaseName = null;
                if (domain.Contains("databaseName"))
                {
                    databaseName = domain.GetValue("databaseName")?.AsString;
                }

                if (string.IsNullOrEmpty(databaseName))
                {
                    // Fallback: construct from name (mng_{name})
                    var domainName = domain.GetValue("name")?.AsString;
                    if (!string.IsNullOrEmpty(domainName))
                    {
                        databaseName = $"mng_{domainName.ToLowerInvariant()}";
                    }
                }

                if (string.IsNullOrEmpty(databaseName))
                {
                    _logger.LogWarning("Database name cannot be determined: DomainId={DomainId}", domainId);
                    return null;
                }

                // Cache the result
                _cache.Set(cacheKey, databaseName, CacheExpiration);
                _logger.LogDebug("Database name cached: DomainId={DomainId}, Database={Database}", domainId, databaseName);

                return databaseName;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting database name: DomainId={DomainId}", domainId);
                return null;
            }
        }

        public Task ClearCacheAsync()
        {
            // MemoryCache doesn't have a direct way to clear all entries with a prefix
            // This would require tracking all keys or using a different cache implementation
            // For now, we'll just log that cache should be cleared manually if needed
            _logger.LogInformation("Cache clear requested - MemoryCache entries will expire naturally");
            return Task.CompletedTask;
        }
    }
}

