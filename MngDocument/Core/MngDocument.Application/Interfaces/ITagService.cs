using MngDocument.Application.Contracts.Tags;

namespace MngDocument.Application.Interfaces;

public interface ITagService
{
    Task<TagListResult> ListAsync(bool activeOnly = false, CancellationToken ct = default);
    Task<TagDto> GetByIdAsync(string id, CancellationToken ct = default);
    Task<TagDto> CreateAsync(CreateTagRequest request, CancellationToken ct = default);
    Task<TagDto> UpdateAsync(string id, UpdateTagRequest request, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Etiket adlarını katalogdaki aktif kayıtlarla eşleştirir; kanonik ad listesi döner.
    /// Boş liste geçerlidir. Bilinmeyen veya pasif etiket → validation hatası.
    /// </summary>
    Task<IReadOnlyList<string>> NormalizeActiveTagNamesAsync(IReadOnlyList<string>? tags, CancellationToken ct = default);
}
