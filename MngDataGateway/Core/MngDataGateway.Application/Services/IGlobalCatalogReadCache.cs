using MngDataGateway.Application.DTOs.Data;

namespace MngDataGateway.Application.Services;

/// <summary>
/// Faz 3 — sık okunan global katalog listeleri için read-through önbellek (domain + dataset kapsamlı).
/// </summary>
public interface IGlobalCatalogReadCache
{
    bool TryGet(
        string databaseName,
        string datasetName,
        QueryOptionsDto options,
        out QueryResultDto? result);

    void Set(
        string databaseName,
        string datasetName,
        QueryOptionsDto options,
        QueryResultDto result);

    void Invalidate(string databaseName, string datasetName);
}
