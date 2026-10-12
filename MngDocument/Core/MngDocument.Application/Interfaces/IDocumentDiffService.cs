using MngDocument.Application.Contracts.Resources;

namespace MngDocument.Application.Interfaces;

/// <summary>İki indeksli belgenin paragraf farkı. Model kullanmaz.</summary>
public interface IDocumentDiffService
{
    Task<ResourceDiffResultDto> CompareAsync(string baseId, IReadOnlyList<string> otherIds, CancellationToken ct = default);
}
