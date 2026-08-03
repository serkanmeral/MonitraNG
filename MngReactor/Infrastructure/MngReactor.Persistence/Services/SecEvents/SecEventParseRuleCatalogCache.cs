using Microsoft.Extensions.Caching.Memory;
using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Persistence.Services.SecEvents;

public sealed class SecEventParseRuleCatalogCache : ISecEventParseRuleCatalogCache
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);
    private readonly ISecEventParseRuleCatalogStore _store;
    private readonly IMemoryCache _cache;

    public SecEventParseRuleCatalogCache(
        ISecEventParseRuleCatalogStore store,
        IMemoryCache cache)
    {
        _store = store;
        _cache = cache;
    }

    public async Task<IReadOnlyList<SecEventParseRuleDocument>> GetEnabledRulesAsync(
        string domain,
        CancellationToken cancellationToken = default)
    {
        var key = CacheKey(domain);
        if (_cache.TryGetValue(key, out IReadOnlyList<SecEventParseRuleDocument>? cached) && cached is not null)
            return cached;

        var db = $"mng_{domain.Trim().ToLowerInvariant()}";
        // Ensure meta/seed exists for empty tenants.
        if (await _store.GetMetaAsync(db, cancellationToken) is null)
        {
            // Lightweight: do not duplicate full EnsureSeeded here; engine still works with empty list.
        }

        var docs = await _store.ListAsync(db, cancellationToken);
        var enabled = docs
            .Where(d => d.Enabled)
            .OrderByDescending(d => d.Priority)
            .ThenBy(d => d.RuleId, StringComparer.Ordinal)
            .ToList();

        _cache.Set(key, (IReadOnlyList<SecEventParseRuleDocument>)enabled, CacheTtl);
        return enabled;
    }

    public void Invalidate(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return;
        _cache.Remove(CacheKey(domain));
    }

    private static string CacheKey(string domain) =>
        $"sec-event-parse-rules:{domain.Trim().ToLowerInvariant()}";
}
