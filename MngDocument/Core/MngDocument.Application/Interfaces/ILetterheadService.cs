using MngDocument.Application.Contracts.Letterheads;
using MngDocument.Application.Contracts.Templates;

namespace MngDocument.Application.Interfaces;

public interface ILetterheadService
{
    Task<LetterheadListResult> ListAsync(bool activeOnly = false, CancellationToken ct = default);
    Task<LetterheadDto> GetByIdAsync(string id, CancellationToken ct = default);
    Task<LetterheadDto?> TryGetByIdAsync(string id, CancellationToken ct = default);
    Task<LetterheadDto?> TryGetByCodeAsync(string code, CancellationToken ct = default);
    Task<LetterheadDto> CreateAsync(CreateLetterheadRequest request, CancellationToken ct = default);
    Task<LetterheadDto> UpdateAsync(string id, UpdateLetterheadRequest request, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
    Task EnsureActiveAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Üretim / şablon branding için antet çözümü:
    /// templateDefaultId → katalog varsayılanı → legacy gömülü model.
    /// </summary>
    Task<LetterheadResolveResult> ResolveAsync(
        string? templateDefaultLetterheadId,
        TemplateLetterheadDto? legacyEmbedded,
        CancellationToken ct = default);
}
