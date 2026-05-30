namespace MngOperations.Application.Interfaces;

/// <summary>
/// Global katalog (states/priorities/types/fields) CRUD'u — MO write-through:
/// DG'ye yazar ve aynı işlemde ilgili cache'i düşürür. Validation passthrough (DG dataset şeması doğrular).
/// </summary>
public interface ICatalogService
{
    Task<IReadOnlyList<Dictionary<string, object?>>> ListAsync(
        string source,
        CancellationToken cancellationToken = default);

    Task<Dictionary<string, object?>> CreateAsync(
        string source,
        Dictionary<string, object?> data,
        CancellationToken cancellationToken = default);

    Task<Dictionary<string, object?>> UpdateAsync(
        string source,
        string id,
        Dictionary<string, object?> data,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string source,
        string id,
        CancellationToken cancellationToken = default);
}
