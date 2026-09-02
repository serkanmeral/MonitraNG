using MngDocument.Application.Contracts.Catalogs;

namespace MngDocument.Application.Interfaces;

public interface IResourceKindCatalog
{
    Task<CatalogListResult<ResourceKindDto>> ListAsync(bool activeOnly = true, CancellationToken ct = default);

    /// <summary>Boş → null. Bilinmeyen kod validation fırlatır.</summary>
    Task<string?> NormalizeAsync(string? code, CancellationToken ct = default);
}

public interface IRelationTypeCatalog
{
    Task<CatalogListResult<RelationTypeDto>> ListAsync(bool activeOnly = true, CancellationToken ct = default);

    Task<bool> IsAllowedAsync(string? code, CancellationToken ct = default);
}
