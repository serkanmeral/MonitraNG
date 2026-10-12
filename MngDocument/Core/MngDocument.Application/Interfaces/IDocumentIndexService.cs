using MngDocument.Application.Contracts.Resources;

namespace MngDocument.Application.Interfaces;

/// <summary>DI düz metin indeksi. Aynı anda etki alanında tek belge.</summary>
public interface IDocumentIndexService
{
    Task<ResourceIndexResultDto> GetStatusAsync(string id, CancellationToken ct = default);

    Task<ResourceIndexResultDto> IndexAsync(string id, CancellationToken ct = default);
}
