using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngDataGateway.Application.Configuration;
using MngDataGateway.Application.DTOs.Data;
using MngDataGateway.Application.Services;

namespace MngDataGateway.Persistence.Services;

/// <summary>
/// op_states / op_priorities / op_work_item_types gibi global katalog listelerini önbelleğe alır.
/// Yalnızca filtre/arama olmayan ilk sayfa listeleri (MO metadata cache deseni).
/// </summary>
public sealed class GlobalCatalogReadCache : IGlobalCatalogReadCache
{
    private static readonly HashSet<string> CatalogDatasets = new(StringComparer.OrdinalIgnoreCase)
    {
        "op_states",
        "op_priorities",
        "op_work_item_types",
        "op_fields",
        "op_tags",
    };

    private readonly IMemoryCache _cache;
    private readonly ILogger<GlobalCatalogReadCache> _logger;
    private readonly TimeSpan _ttl;
    private readonly ConcurrentDictionary<string, ConcurrentBag<string>> _keysByRegistry = new();

    public GlobalCatalogReadCache(
        IMemoryCache cache,
        ILogger<GlobalCatalogReadCache> logger,
        IOptions<MngDataGatewaySettings> settings)
    {
        _cache = cache;
        _logger = logger;
        var ttlSeconds = settings.Value.CatalogReadCache?.TtlSeconds ?? 300;
        _ttl = TimeSpan.FromSeconds(Math.Max(30, ttlSeconds));
    }

    public bool TryGet(
        string databaseName,
        string datasetName,
        QueryOptionsDto options,
        out QueryResultDto? result)
    {
        result = null;
        if (!IsCacheableQuery(datasetName, options))
            return false;

        var key = BuildKey(databaseName, datasetName, options);
        if (_cache.TryGetValue(key, out QueryResultDto? cached) && cached != null)
        {
            result = cached;
            return true;
        }

        return false;
    }

    public void Set(
        string databaseName,
        string datasetName,
        QueryOptionsDto options,
        QueryResultDto result)
    {
        if (!IsCacheableQuery(datasetName, options))
            return;

        var key = BuildKey(databaseName, datasetName, options);
        _cache.Set(key, result, _ttl);
        _keysByRegistry.GetOrAdd(RegistryKey(databaseName, datasetName), _ => new ConcurrentBag<string>()).Add(key);
        _logger.LogDebug("Catalog read cache set {Key} ({Count} rows)", key, result.Data?.Count ?? 0);
    }

    public void Invalidate(string databaseName, string datasetName)
    {
        if (!CatalogDatasets.Contains(datasetName))
            return;

        var registry = RegistryKey(databaseName, datasetName);
        if (!_keysByRegistry.TryRemove(registry, out var keys))
            return;

        var removed = 0;
        foreach (var key in keys)
        {
            _cache.Remove(key);
            removed++;
        }

        if (removed > 0)
            _logger.LogDebug("Catalog read cache invalidated {Registry} ({Count} keys)", registry, removed);
    }

    private static bool IsCacheableQuery(string datasetName, QueryOptionsDto options)
    {
        if (!CatalogDatasets.Contains(datasetName))
            return false;
        if (options.Skip != 0)
            return false;
        if (!string.IsNullOrWhiteSpace(options.Filter))
            return false;
        if (!string.IsNullOrWhiteSpace(options.Search))
            return false;
        if (!string.IsNullOrWhiteSpace(options.Fields))
            return false;
        if (options.ShowHistory || options.ShowQuery || options.ShowDataset)
            return false;
        if (!string.Equals(options.Format, "json", StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    private static string RegistryKey(string databaseName, string datasetName) =>
        $"{databaseName}::{datasetName}";

    private static string BuildKey(string databaseName, string datasetName, QueryOptionsDto options)
    {
        var sort = options.Sort?.Trim() ?? "";
        return $"{databaseName}::{datasetName}::l{options.Limit}::e{options.Expand}::d{options.Deep}::s{sort}";
    }
}
