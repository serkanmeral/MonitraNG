using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MngOperations.Application.Utilities;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public partial class MetadataCacheService
{
    private static readonly HashSet<string> CatalogRelationDatasets = new(StringComparer.Ordinal)
    {
        OcDatasets.States,
        OcDatasets.Priorities,
        OcDatasets.WorkItemTypes,
        OcDatasets.Boards,
        OcDatasets.Tags,
    };

    private static readonly string[] CorePersonFieldKeys = { "assignee", "watchers" };
    private static readonly string[] CoreGroupFieldKeys = { "assignmentGroups" };

    public async Task<IReadOnlyList<Dictionary<string, object?>>> GetWorkspacePoolFieldsAsync(
        string workspaceId,
        string token,
        CancellationToken cancellationToken = default)
    {
        var wsId = workspaceId?.Trim()
            ?? throw new ArgumentException("workspaceId is required.", nameof(workspaceId));

        var cacheKey = CacheKey($"poolfields:{wsId}");
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<Dictionary<string, object?>>? cached) && cached != null)
            return cached;

        var fields = await GetCatalogListAsync(OcDatasets.Fields, token, cancellationToken);
        var result = new List<Dictionary<string, object?>>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var f in fields)
        {
            var scope = (WorkItemDataHelper.GetString(f, "scope") ?? "pool").Trim();
            if (!string.Equals(scope, "pool", StringComparison.OrdinalIgnoreCase))
                continue;

            var dataId = WorkItemDataHelper.GetString(f, "__dataId");
            var key = WorkItemDataHelper.GetString(f, "key");
            if (string.IsNullOrWhiteSpace(dataId) || string.IsNullOrWhiteSpace(key) || !seen.Add(dataId))
                continue;

            var fieldWs = WorkItemDataHelper.GetPersonRefId(f, "workspaceId");
            if (string.IsNullOrWhiteSpace(fieldWs) || string.Equals(fieldWs, wsId, StringComparison.Ordinal))
                result.Add(f);
        }

        var frozen = (IReadOnlyList<Dictionary<string, object?>>)result;
        _cache.Set(cacheKey, frozen, _ttl);
        return frozen;
    }

    public Task<IReadOnlyList<string>> GetPersonPoolFieldKeysAsync(
        string token,
        CancellationToken cancellationToken = default) =>
        GetPoolFieldKeysByTypeAsync(
            CacheKey("poolkeys:person"),
            new[] { "persons", "person" },
            CorePersonFieldKeys,
            token,
            cancellationToken);

    public Task<IReadOnlyList<string>> GetGroupPoolFieldKeysAsync(
        string token,
        CancellationToken cancellationToken = default) =>
        GetPoolFieldKeysByTypeAsync(
            CacheKey("poolkeys:group"),
            new[] { "persongroups", "persongroup", "group" },
            CoreGroupFieldKeys,
            token,
            cancellationToken);

    public async Task<IReadOnlyDictionary<string, string>> ResolveRelationDisplayNamesAsync(
        string dataset,
        string labelField,
        IReadOnlyCollection<string> ids,
        string token,
        CancellationToken cancellationToken = default)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (ids.Count == 0)
            return map;

        var missing = new List<string>();
        foreach (var id in ids)
        {
            if (string.IsNullOrWhiteSpace(id))
                continue;

            var trimmed = id.Trim();
            var entryKey = RelationNameCacheKey(dataset, labelField, trimmed);
            if (_cache.TryGetValue(entryKey, out string? cached) && !string.IsNullOrWhiteSpace(cached))
                map[trimmed] = cached!;
            else if (!map.ContainsKey(trimmed))
                missing.Add(trimmed);
        }

        if (missing.Count == 0)
            return map;

        Dictionary<string, string> fetched;
        if (CatalogRelationDatasets.Contains(dataset))
            fetched = await ResolveCatalogDisplayNamesCoreAsync(dataset, missing, token, cancellationToken);
        else
            fetched = await QueryRelationDisplayNamesCoreAsync(dataset, labelField, missing, token, cancellationToken);

        foreach (var kv in fetched)
        {
            map[kv.Key] = kv.Value;
            _cache.Set(RelationNameCacheKey(dataset, labelField, kv.Key), kv.Value, _catalogTtl);
        }

        return map;
    }

    private async Task<IReadOnlyList<string>> GetPoolFieldKeysByTypeAsync(
        string cacheKey,
        IReadOnlyList<string> fieldTypes,
        IReadOnlyList<string> coreKeys,
        string token,
        CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<string>? cached) && cached != null)
            return cached;

        var keys = new List<string>(coreKeys);
        try
        {
            var fields = await GetCatalogListAsync(OcDatasets.Fields, token, cancellationToken);
            foreach (var field in fields)
            {
                var fieldType = WorkItemDataHelper.GetString(field, "fieldType")?.Trim().ToLowerInvariant();
                if (fieldType == null || !fieldTypes.Contains(fieldType))
                    continue;

                var key = WorkItemDataHelper.GetString(field, "key");
                if (!string.IsNullOrWhiteSpace(key) && !keys.Contains(key))
                    keys.Add(key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pool field keys resolve failed for {CacheKey}.", cacheKey);
        }

        var frozen = (IReadOnlyList<string>)keys;
        _cache.Set(cacheKey, frozen, _catalogTtl);
        return frozen;
    }

    private async Task<Dictionary<string, string>> ResolveCatalogDisplayNamesCoreAsync(
        string dataset,
        IReadOnlyList<string> ids,
        string token,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        var wanted = new HashSet<string>(ids, StringComparer.Ordinal);
        var rows = await GetCatalogListAsync(dataset, token, cancellationToken);
        foreach (var row in rows)
        {
            var id = WorkItemDataHelper.GetString(row, "__dataId");
            if (string.IsNullOrWhiteSpace(id) || !wanted.Contains(id))
                continue;

            var name = WorkItemDataHelper.GetString(row, "name")
                ?? WorkItemDataHelper.GetString(row, "label")
                ?? WorkItemDataHelper.GetString(row, "title");
            if (!string.IsNullOrWhiteSpace(name))
                map[id] = name!;
        }

        return map;
    }

    private async Task<Dictionary<string, string>> QueryRelationDisplayNamesCoreAsync(
        string dataset,
        string labelField,
        IReadOnlyList<string> ids,
        string token,
        CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (ids.Count == 0)
            return map;

        var match = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["__dataId"] = new Dictionary<string, object?> { ["$in"] = ids.Cast<object?>().ToList() }
        };
        var page = await _dg.QueryPageAsync(
            dataset,
            match,
            $"limit={Math.Max(ids.Count, 1)}&expand=false",
            token,
            cancellationToken);

        foreach (var row in page.Items)
        {
            var id = WorkItemDataHelper.GetString(row, "__dataId");
            if (string.IsNullOrWhiteSpace(id))
                continue;

            var name = LookupFieldOptionsHelper.ResolveDisplayText(row, labelField);
            if (!string.IsNullOrWhiteSpace(name))
                map[id!] = name!;
        }

        return map;
    }

    private string RelationNameCacheKey(string dataset, string labelField, string id) =>
        CacheKey($"relname:{dataset}:{labelField}:{id}");
}
