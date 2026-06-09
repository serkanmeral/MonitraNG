using MngDocument.Application.Contracts.Resources;

namespace MngDocument.Application.Interfaces;

/// <summary>
/// Document Intelligence Faz 1 kaynak orkestrasyonu (klasör ağacı, markdown, dosya metadata,
/// taşıma, arama). Kalıcılık DG üzerinden; yetki Faz 1'de minimum (domain içi açık).
/// </summary>
public interface IResourceService
{
    Task<IReadOnlyList<TreeNodeDto>> GetTreeAsync(CancellationToken ct = default);

    /// <summary>Ağaç + kök veya <paramref name="folderId"/> altı içerik (tek permission snapshot).</summary>
    Task<ResourceBootstrapDto> GetBootstrapAsync(string? folderId = null, CancellationToken ct = default);

    /// <summary>Klasör gezinme paketi: children + breadcrumb + seçili klasör (tek snapshot).</summary>
    Task<ResourceBrowseContextDto> GetBrowseContextAsync(string? folderId, CancellationToken ct = default);

    Task<ResourceListResult> GetChildrenAsync(string? parentId, CancellationToken ct = default);

    Task<ResourceDto> GetByIdAsync(string id, CancellationToken ct = default);

    Task<IReadOnlyList<BreadcrumbDto>> GetBreadcrumbAsync(string id, CancellationToken ct = default);

    Task<ResourceDto> CreateFolderAsync(CreateFolderRequest request, CancellationToken ct = default);

    Task<ResourceDto> RenameAsync(string id, RenameResourceRequest request, CancellationToken ct = default);

    Task<ResourceDto> MoveAsync(string id, MoveResourceRequest request, CancellationToken ct = default);

    Task DeleteAsync(string id, bool force, CancellationToken ct = default);

    Task<ResourceDto> CreateMarkdownAsync(CreateMarkdownRequest request, CancellationToken ct = default);

    Task<ResourceDto> UpdateMarkdownAsync(string id, UpdateMarkdownRequest request, CancellationToken ct = default);

    Task<MarkdownContentDto> GetMarkdownContentAsync(string id, CancellationToken ct = default);

    Task<IReadOnlyList<MarkdownVersionDto>> GetMarkdownVersionsAsync(string id, CancellationToken ct = default);

    Task<MarkdownVersionContentDto> GetMarkdownVersionContentAsync(string id, int versionNumber, CancellationToken ct = default);

    Task<ResourceDto> RestoreMarkdownVersionAsync(string id, int versionNumber, CancellationToken ct = default);

    Task<ResourceDto> CreateFileResourceAsync(CreateFileResourceRequest request, CancellationToken ct = default);

    Task<ResourceListResult> SearchAsync(string query, int skip, int limit, CancellationToken ct = default);

    /// <summary>Son güncellenen yayınlanmış markdown kayıtları (widget P2).</summary>
    Task<ResourceListResult> GetRecentAsync(int limit, CancellationToken ct = default);

    /// <summary>Kullanıcının düzenleyebildiği taslak markdown kayıtları (widget P2).</summary>
    Task<ResourceListResult> GetDraftsAsync(int limit, CancellationToken ct = default);
}
