using MngDocument.Application.Contracts.Resources;

namespace MngDocument.Application.Interfaces;

/// <summary>İndeksli belgelerde ad ve içerik araması. OpenSearch aday verir, yetki sonra elenir.</summary>
public interface IDocumentContentSearchService
{
    Task<ContentSearchResultDto> SearchAsync(
        string query,
        string scope,
        string? scopeId,
        string? extension,
        string match,
        string sort,
        CancellationToken ct = default);
}

/// <summary>DI düz metin indeksi. Aynı anda etki alanında tek belge.</summary>
public interface IDocumentIndexService
{
    Task<ResourceIndexResultDto> GetStatusAsync(string id, CancellationToken ct = default);

    Task<ResourceIndexResultDto> IndexAsync(string id, CancellationToken ct = default);

    /// <summary>Klasör ve alt klasörlerdeki uygun dosyaları sırayla indeksler. Tek etki alanı işi.</summary>
    Task<BulkIndexJobDto> StartBatchAsync(string folderId, CancellationToken ct = default);

    Task<BulkIndexJobDto> GetBatchAsync(string folderId, CancellationToken ct = default);

    Task<BulkIndexJobDto> CancelBatchAsync(string folderId, CancellationToken ct = default);
}
