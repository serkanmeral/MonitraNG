using Microsoft.Extensions.Logging;
using MngOperations.Application.Catalogs;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Interfaces;

namespace MngOperations.Infrastructure.Services;

/// <summary>
/// Global katalog CRUD'u tek noktadan yönetir: DG'ye yazar, MO cache'ini write-through düşürür,
/// silmeden önce kullanım guard'ı uygular. Validation DG dataset şemasına bırakılır (passthrough).
/// </summary>
public sealed class CatalogService : ICatalogService
{
    private readonly IMngDataGatewayClient _dg;
    private readonly IMetadataCache _metadataCache;
    private readonly IRequestContext _requestContext;
    private readonly ILogger<CatalogService> _logger;

    public CatalogService(
        IMngDataGatewayClient dg,
        IMetadataCache metadataCache,
        IRequestContext requestContext,
        ILogger<CatalogService> logger)
    {
        _dg = dg;
        _metadataCache = metadataCache;
        _requestContext = requestContext;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> ListAsync(
        string source,
        CancellationToken cancellationToken = default)
    {
        var definition = ResolveSource(source);
        var token = RequireToken();
        return await _metadataCache.GetCatalogListAsync(definition.Dataset, token, cancellationToken);
    }

    public async Task<Dictionary<string, object?>> CreateAsync(
        string source,
        Dictionary<string, object?> data,
        CancellationToken cancellationToken = default)
    {
        var definition = ResolveSource(source);
        var token = RequireToken();
        EnsureCatalogAdmin();

        var payload = Sanitize(data);
        if (payload.Count == 0)
            throw new OperationCoreException("CATALOG_EMPTY_PAYLOAD", "Catalog payload is empty.", "Katalog verisi boş.", 400);

        var created = await _dg.CreateAsync(definition.Dataset, payload, token, cancellationToken);
        _metadataCache.InvalidateCatalog(definition.Dataset);
        _logger.LogInformation("Catalog created source={Source} dataset={Dataset}", definition.Source, definition.Dataset);
        return created;
    }

    public async Task<Dictionary<string, object?>> UpdateAsync(
        string source,
        string id,
        Dictionary<string, object?> data,
        CancellationToken cancellationToken = default)
    {
        var definition = ResolveSource(source);
        var token = RequireToken();
        EnsureCatalogAdmin();
        EnsureId(id);

        var payload = Sanitize(data);
        var updated = await _dg.UpdateAsync(definition.Dataset, id.Trim(), payload, token, cancellationToken);
        _metadataCache.InvalidateCatalog(definition.Dataset);
        _logger.LogInformation("Catalog updated source={Source} dataset={Dataset} id={Id}", definition.Source, definition.Dataset, id);
        return updated;
    }

    public async Task DeleteAsync(
        string source,
        string id,
        CancellationToken cancellationToken = default)
    {
        var definition = ResolveSource(source);
        var token = RequireToken();
        EnsureCatalogAdmin();
        EnsureId(id);

        await EnsureNotInUseAsync(definition, id.Trim(), token, cancellationToken);

        var deleted = await _dg.DeleteAsync(definition.Dataset, id.Trim(), token, cancellationToken);
        if (!deleted)
            throw new OperationCoreException("CATALOG_NOT_FOUND", "Catalog item not found.", "Katalog kaydı bulunamadı.", 404);

        _metadataCache.InvalidateCatalog(definition.Dataset);
        _logger.LogInformation("Catalog deleted source={Source} dataset={Dataset} id={Id}", definition.Source, definition.Dataset, id);
    }

    private async Task EnsureNotInUseAsync(
        CatalogDefinition definition,
        string id,
        string token,
        CancellationToken cancellationToken)
    {
        foreach (var check in definition.UsageChecks)
        {
            var filter = Uri.EscapeDataString($"{check.Field}:eq:{id}");
            var rows = await _dg.GetAsync<Dictionary<string, object?>>(
                check.Dataset,
                $"filter={filter}&limit=1",
                token,
                cancellationToken);

            if (rows.Any())
            {
                throw new OperationCoreException(
                    "CATALOG_IN_USE",
                    $"Catalog item is referenced by {check.UsageKey}.",
                    "Katalog kaydı kullanımda olduğu için silinemez.",
                    409,
                    new Dictionary<string, object?>
                    {
                        ["usage"] = check.UsageKey,
                        ["dataset"] = check.Dataset,
                        ["field"] = check.Field,
                    });
            }
        }
    }

    private static CatalogDefinition ResolveSource(string source)
    {
        if (OcCatalogRegistry.TryResolve(source, out var definition))
            return definition;

        throw new OperationCoreException(
            "CATALOG_SOURCE_UNKNOWN",
            $"Unknown catalog source '{source}'.",
            "Bilinmeyen katalog kaynağı.",
            400,
            new Dictionary<string, object?> { ["allowed"] = OcCatalogRegistry.Sources });
    }

    private static Dictionary<string, object?> Sanitize(Dictionary<string, object?> data)
    {
        if (data == null)
            return new Dictionary<string, object?>();

        return data
            .Where(kv => !kv.Key.StartsWith("__", StringComparison.Ordinal))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    private static void EnsureId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new OperationCoreException("CATALOG_ID_REQUIRED", "Catalog id is required.", "Katalog id gerekli.", 400);
    }

    private void EnsureCatalogAdmin()
    {
        // Platform admin (IsAdmin) veya yönetici (IsManager) — workspace admin aksiyonlarıyla aynı seviye.
        if (!_requestContext.IsAdmin && !_requestContext.IsManager)
            throw new OperationCoreException(
                "CATALOG_FORBIDDEN",
                "Catalog management requires admin privileges.",
                "Katalog yönetimi için yönetici yetkisi gerekli.",
                403);
    }

    private string RequireToken()
    {
        if (string.IsNullOrEmpty(_requestContext.BearerToken))
            throw new OperationCoreException("UNAUTHORIZED", "Bearer token is required.", "Bearer token gerekli.", 401);

        return _requestContext.BearerToken;
    }
}
