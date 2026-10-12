using MngDocument.Application.Contracts.Resources;

namespace MngDocument.Application.Interfaces;

/// <summary>İndeksli belgenin saklanan özeti. Üretim indeks ile aynı tek iş kilidini kullanır.</summary>
public interface IDocumentSummaryService
{
    Task<ResourceSummaryResultDto> GetAsync(string id, CancellationToken ct = default);

    Task<ResourceSummaryResultDto> StartAsync(string id, CancellationToken ct = default);
}
