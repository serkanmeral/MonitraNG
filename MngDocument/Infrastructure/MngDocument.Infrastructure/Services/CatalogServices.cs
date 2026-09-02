using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using MngDocument.Application.Contracts.Catalogs;
using MngDocument.Application.Exceptions;
using MngDocument.Application.Interfaces;
using MngDocument.Application.Models;
using MngDocument.Domain.Constants;

namespace MngDocument.Infrastructure.Services;

public sealed class ResourceKindCatalog : IResourceKindCatalog
{
    private const string ListQuery = "skip=0&limit=200&expand=false&showHistory=false";
    private const string CacheKey = "dm_resource_kinds";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IMngDataGatewayClient _dg;
    private readonly IRequestContext _ctx;
    private readonly IMemoryCache _cache;

    public ResourceKindCatalog(IMngDataGatewayClient dg, IRequestContext ctx, IMemoryCache cache)
    {
        _dg = dg;
        _ctx = ctx;
        _cache = cache;
    }

    public async Task<CatalogListResult<ResourceKindDto>> ListAsync(bool activeOnly = true, CancellationToken ct = default)
    {
        var items = await LoadAsync(ct);
        if (activeOnly)
            items = items.Where(x => x.IsActive).ToList();
        return new CatalogListResult<ResourceKindDto> { Items = items, Total = items.Count };
    }

    public async Task<string?> NormalizeAsync(string? code, CancellationToken ct = default)
    {
        var trimmed = code?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return null;

        var items = await LoadAsync(ct);
        var match = items.FirstOrDefault(x =>
            x.IsActive && string.Equals(x.Code, trimmed, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            throw DocumentException.Validation(
                "RESOURCE_KIND_INVALID",
                "Unknown resource kind.",
                "Geçersiz kaynak türü.");
        }

        return match.Code;
    }

    private async Task<List<ResourceKindDto>> LoadAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(CacheKey, out List<ResourceKindDto>? cached) && cached is not null)
            return cached;

        try
        {
            var page = await _dg.QueryPageAsync(
                DmDatasets.ResourceKinds,
                new Dictionary<string, object?>(),
                ListQuery,
                _ctx.BearerToken,
                ct);

            var items = page.Items
                .Select(Map)
                .Where(x => !string.IsNullOrWhiteSpace(x.Code))
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (items.Count == 0)
                items = BuiltInList();

            _cache.Set(CacheKey, items, TimeSpan.FromMinutes(2));
            return items;
        }
        catch
        {
            return BuiltInList();
        }
    }

    private static List<ResourceKindDto> BuiltInList() =>
        ResourceKindCode.BuiltIn
            .Select(x => new ResourceKindDto
            {
                Code = x.Code,
                DisplayName = x.DisplayName,
                Family = x.Family,
                SortOrder = x.SortOrder,
                IsActive = true
            })
            .ToList();

    private static ResourceKindDto Map(Dictionary<string, object?> row)
    {
        var rec = JsonSerializer.Deserialize<DmResourceKind>(JsonSerializer.Serialize(row, JsonOptions), JsonOptions)
            ?? new DmResourceKind();
        return new ResourceKindDto
        {
            Id = rec.__dataId ?? string.Empty,
            Code = rec.code?.Trim() ?? string.Empty,
            DisplayName = rec.displayName?.Trim() ?? rec.code?.Trim() ?? string.Empty,
            Description = rec.description,
            Family = rec.family,
            SortOrder = rec.sortOrder ?? 0,
            IsActive = rec.isActive != false
        };
    }
}

public sealed class RelationTypeCatalog : IRelationTypeCatalog
{
    private const string ListQuery = "skip=0&limit=200&expand=false&showHistory=false";
    private const string CacheKey = "dm_relation_types";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IMngDataGatewayClient _dg;
    private readonly IRequestContext _ctx;
    private readonly IMemoryCache _cache;

    public RelationTypeCatalog(IMngDataGatewayClient dg, IRequestContext ctx, IMemoryCache cache)
    {
        _dg = dg;
        _ctx = ctx;
        _cache = cache;
    }

    public async Task<CatalogListResult<RelationTypeDto>> ListAsync(bool activeOnly = true, CancellationToken ct = default)
    {
        var items = await LoadAsync(ct);
        if (activeOnly)
            items = items.Where(x => x.IsActive).ToList();
        return new CatalogListResult<RelationTypeDto> { Items = items, Total = items.Count };
    }

    public async Task<bool> IsAllowedAsync(string? code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;
        if (ResourceLinkRelationType.IsBuiltIn(code))
            return true;

        var items = await LoadAsync(ct);
        return items.Any(x =>
            x.IsActive && string.Equals(x.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private async Task<List<RelationTypeDto>> LoadAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue(CacheKey, out List<RelationTypeDto>? cached) && cached is not null)
            return cached;

        try
        {
            var page = await _dg.QueryPageAsync(
                DmDatasets.RelationTypes,
                new Dictionary<string, object?>(),
                ListQuery,
                _ctx.BearerToken,
                ct);

            var items = page.Items
                .Select(Map)
                .Where(x => !string.IsNullOrWhiteSpace(x.Code))
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (items.Count == 0)
                items = BuiltInList();
            _cache.Set(CacheKey, items, TimeSpan.FromMinutes(2));
            return items;
        }
        catch
        {
            return BuiltInList();
        }
    }

    private static List<RelationTypeDto> BuiltInList() =>
        ResourceLinkRelationType.BuiltIn
            .Select(code => new RelationTypeDto { Code = code, DisplayName = code, IsActive = true })
            .ToList();

    private static RelationTypeDto Map(Dictionary<string, object?> row)
    {
        var rec = JsonSerializer.Deserialize<DmRelationType>(JsonSerializer.Serialize(row, JsonOptions), JsonOptions)
            ?? new DmRelationType();
        return new RelationTypeDto
        {
            Id = rec.__dataId ?? string.Empty,
            Code = rec.code?.Trim() ?? string.Empty,
            DisplayName = rec.displayName?.Trim() ?? rec.code?.Trim() ?? string.Empty,
            Description = rec.description,
            AppliesTo = rec.appliesTo,
            SortOrder = rec.sortOrder ?? 0,
            IsActive = rec.isActive != false
        };
    }
}

