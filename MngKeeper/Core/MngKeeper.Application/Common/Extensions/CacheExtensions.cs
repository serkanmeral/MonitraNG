using MngKeeper.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace MngKeeper.Application.Common.Extensions
{
    /// <summary>
    /// Extension methods for cache operations
    /// </summary>
    public static class CacheExtensions
    {
        /// <summary>
        /// Gets a value from cache or executes the factory function and caches the result
        /// </summary>
        public static async Task<T> GetOrSetAsync<T>(
            this IRedisService cacheService,
            string cacheKey,
            Func<Task<T>> factory,
            TimeSpan? expiry = null,
            ILogger? logger = null) where T : class
        {
            // Try to get from cache
            var cached = await cacheService.GetAsync<T>(cacheKey);
            if (cached != null)
            {
                logger?.LogDebug("Cache hit: {CacheKey}", cacheKey);
                return cached;
            }

            logger?.LogDebug("Cache miss: {CacheKey}", cacheKey);

            // Execute factory function
            var result = await factory();

            // Cache the result
            if (result != null)
            {
                await cacheService.SetAsync(cacheKey, result, expiry);
                logger?.LogDebug("Cached result: {CacheKey}", cacheKey);
            }

            return result ?? throw new InvalidOperationException("Factory function returned null");
        }

        /// <summary>
        /// Builds a cache key for paginated queries
        /// </summary>
        public static string BuildCacheKey(
            string prefix,
            string domainId,
            int page,
            int pageSize,
            string? searchTerm = null,
            bool? isActive = null)
        {
            return $"{prefix}:domain:{domainId}:page:{page}:size:{pageSize}:search:{searchTerm ?? "null"}:active:{isActive?.ToString() ?? "null"}";
        }

        /// <summary>
        /// Invalidates cache keys matching a pattern (for a specific domain)
        /// Note: Redis doesn't support wildcard deletion directly, this is a placeholder
        /// For production, consider using Redis SCAN or cache tags
        /// </summary>
        public static async Task InvalidateDomainCacheAsync(
            this IRedisService cacheService,
            string prefix,
            string domainId,
            ILogger? logger = null)
        {
            // TODO: Implement proper cache invalidation using Redis SCAN or cache tags
            // For now, cache will expire naturally based on TTL
            logger?.LogDebug("Cache invalidation requested for {Prefix} in domain {DomainId} (TTL-based expiration will handle this)", prefix, domainId);
            await Task.CompletedTask;
        }
    }
}

