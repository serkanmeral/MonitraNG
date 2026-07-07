using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngDocument.Application.Contracts.EditorSessions;
using MngDocument.Application.Contracts.Resources;
using MngDocument.Application.Interfaces;
using MngDocument.Domain.Constants;

namespace MngDocument.Api.Controllers;

/// <summary>
/// Document Intelligence Faz 1 kaynak ağacı API'si: klasör CRUD, markdown doküman,
/// dosya metadata kaydı, taşıma ve arama. Binary upload/download DG <c>files</c> uçları
/// üzerinden yapılır (bu servis binary proxy yapmaz).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/resources")]
[Authorize]
public class ResourcesController : ControllerBase
{
    private readonly IResourceService _resources;
    private readonly IPermissionService _permissions;
    private readonly IResourceEditorService _resourceEditor;

    public ResourcesController(
        IResourceService resources,
        IPermissionService permissions,
        IResourceEditorService resourceEditor)
    {
        _resources = resources;
        _permissions = permissions;
        _resourceEditor = resourceEditor;
    }

    /// <summary>Klasör ağacı (yalnızca klasörler, iç içe).</summary>
    [HttpGet("tree")]
    [ProducesResponseType(typeof(IReadOnlyList<TreeNodeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTree(CancellationToken ct) =>
        Ok(await _resources.GetTreeAsync(ct));

    /// <summary>Lazy tree kök seviyesi.</summary>
    [HttpGet("tree/roots")]
    [ProducesResponseType(typeof(IReadOnlyList<TreeNodeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTreeRoots(CancellationToken ct) =>
        Ok(await _resources.GetTreeRootsAsync(ct));

    /// <summary>Lazy tree: bir klasörün alt klasörleri. parentId boşsa kök.</summary>
    [HttpGet("tree/children")]
    [ProducesResponseType(typeof(IReadOnlyList<TreeNodeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTreeChildren([FromQuery] string? parentId, CancellationToken ct) =>
        Ok(await _resources.GetTreeChildrenAsync(parentId, ct));

    /// <summary>Derin link: breadcrumb + yol boyunca kardeş klasör segmentleri.</summary>
    [HttpGet("tree/path")]
    [ProducesResponseType(typeof(TreePathDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTreePath([FromQuery] string folderId, CancellationToken ct) =>
        Ok(await _resources.GetTreePathAsync(folderId, ct));

    /// <summary>Taşı/klon picker: klasör adı araması (yalnızca görülebilir klasörler).</summary>
    [HttpGet("tree/search")]
    [ProducesResponseType(typeof(IReadOnlyList<TreeNodeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchTreeFolders(
        [FromQuery] string q,
        [FromQuery] int limit = 50,
        CancellationToken ct = default) =>
        Ok(await _resources.SearchTreeFoldersAsync(q, limit, ct));

    /// <summary>İlk yükleme / yenileme: ağaç + içerik listesi (tek permission snapshot). folderId verilirse breadcrumb + seçili klasör dahil.</summary>
    [HttpGet("bootstrap")]
    [ProducesResponseType(typeof(ResourceBootstrapDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBootstrap(
        [FromQuery] string? folderId,
        [FromQuery] int skip = 0,
        [FromQuery] int? limit = null,
        CancellationToken ct = default) =>
        Ok(await _resources.GetBootstrapAsync(folderId, skip, limit, ct));

    /// <summary>Klasör gezinme: içerik + breadcrumb + seçili klasör (ağaç hariç, tek snapshot).</summary>
    [HttpGet("browse")]
    [ProducesResponseType(typeof(ResourceBrowseContextDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBrowse(
        [FromQuery] string? folderId,
        [FromQuery] int skip = 0,
        [FromQuery] int? limit = null,
        CancellationToken ct = default) =>
        Ok(await _resources.GetBrowseContextAsync(folderId, skip, limit, ct));

    /// <summary>Bir klasörün içeriği (klasör + markdown + dosya). parentId boşsa kök.</summary>
    [HttpGet("children")]
    [ProducesResponseType(typeof(ResourceListResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChildren(
        [FromQuery] string? parentId,
        [FromQuery] int skip = 0,
        [FromQuery] int? limit = null,
        CancellationToken ct = default) =>
        Ok(await _resources.GetChildrenAsync(parentId, skip, limit, ct));

    /// <summary>Full-text arama (DG regex search: ad/başlık/açıklama + markdown içeriği).</summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(ResourceListResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] int skip = 0,
        [FromQuery] int limit = 50,
        CancellationToken ct = default) =>
        Ok(await _resources.SearchAsync(q, skip, limit, ct));

    /// <summary>Son güncellenen yayınlanmış markdown kayıtları.</summary>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(ResourceListResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecent([FromQuery] int limit = 10, CancellationToken ct = default) =>
        Ok(await _resources.GetRecentAsync(limit, ct));

    /// <summary>Kullanıcının düzenleyebildiği taslak markdown kayıtları.</summary>
    [HttpGet("drafts")]
    [ProducesResponseType(typeof(ResourceListResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDrafts([FromQuery] int limit = 50, CancellationToken ct = default) =>
        Ok(await _resources.GetDraftsAsync(limit, ct));

    /// <summary>Tek kaynak metadata'sı.</summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ResourceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id, CancellationToken ct) =>
        Ok(await _resources.GetByIdAsync(id, ct));

    /// <summary>Kök -> ... -> kaynak yol bilgisi (breadcrumb).</summary>
    [HttpGet("{id}/breadcrumb")]
    [ProducesResponseType(typeof(IReadOnlyList<BreadcrumbDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBreadcrumb(string id, CancellationToken ct) =>
        Ok(await _resources.GetBreadcrumbAsync(id, ct));

    [HttpPost("folder")]
    [ProducesResponseType(typeof(ResourceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateFolder([FromBody] CreateFolderRequest request, CancellationToken ct)
    {
        var created = await _resources.CreateFolderAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id}/rename")]
    [ProducesResponseType(typeof(ResourceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Rename(string id, [FromBody] RenameResourceRequest request, CancellationToken ct) =>
        Ok(await _resources.RenameAsync(id, request, ct));

    [HttpPatch("{id}/metadata")]
    [ProducesResponseType(typeof(ResourceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMetadata(string id, [FromBody] UpdateResourceMetadataRequest request, CancellationToken ct) =>
        Ok(await _resources.UpdateMetadataAsync(id, request, ct));

    [HttpPut("{id}/move")]
    [ProducesResponseType(typeof(ResourceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Move(string id, [FromBody] MoveResourceRequest request, CancellationToken ct) =>
        Ok(await _resources.MoveAsync(id, request, ct));

    /// <summary>Markdown sayfa veya manual DOCX klonlar.</summary>
    [HttpPost("{id}/clone")]
    [ProducesResponseType(typeof(ResourceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Clone(string id, [FromBody] CloneResourceRequest request, CancellationToken ct)
    {
        var created = await _resources.CloneResourceAsync(id, request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(string id, [FromQuery] bool force, CancellationToken ct)
    {
        await _resources.DeleteAsync(id, force, ct);
        return NoContent();
    }

    [HttpPost("markdown")]
    [ProducesResponseType(typeof(ResourceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateMarkdown([FromBody] CreateMarkdownRequest request, CancellationToken ct)
    {
        var created = await _resources.CreateMarkdownAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("markdown/{id}")]
    [ProducesResponseType(typeof(ResourceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateMarkdown(string id, [FromBody] UpdateMarkdownRequest request, CancellationToken ct) =>
        Ok(await _resources.UpdateMarkdownAsync(id, request, ct));

    [HttpGet("markdown/{id}/content")]
    [ProducesResponseType(typeof(MarkdownContentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMarkdownContent(string id, CancellationToken ct) =>
        Ok(await _resources.GetMarkdownContentAsync(id, ct));

    /// <summary>Markdown sürüm geçmişi (içerik hariç, en yeni önce).</summary>
    [HttpGet("markdown/{id}/versions")]
    [ProducesResponseType(typeof(IReadOnlyList<MarkdownVersionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMarkdownVersions(string id, CancellationToken ct) =>
        Ok(await _resources.GetMarkdownVersionsAsync(id, ct));

    /// <summary>Tek bir markdown sürümünün içeriği.</summary>
    [HttpGet("markdown/{id}/versions/{versionNumber:int}")]
    [ProducesResponseType(typeof(MarkdownVersionContentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMarkdownVersionContent(string id, int versionNumber, CancellationToken ct) =>
        Ok(await _resources.GetMarkdownVersionContentAsync(id, versionNumber, ct));

    /// <summary>Eski bir sürümü yeni sürüm olarak geri yükler.</summary>
    [HttpPost("markdown/{id}/versions/{versionNumber:int}/restore")]
    [ProducesResponseType(typeof(ResourceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RestoreMarkdownVersion(string id, int versionNumber, CancellationToken ct) =>
        Ok(await _resources.RestoreMarkdownVersionAsync(id, versionNumber, ct));

    /// <summary>Bu sayfaya markdown iç linki veren diğer sayfalar (backlink).</summary>
    [HttpGet("markdown/{id}/backlinks")]
    [ProducesResponseType(typeof(ResourceListResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMarkdownBacklinks(string id, CancellationToken ct) =>
        Ok(await _resources.GetMarkdownBacklinksAsync(id, ct));

    /// <summary>Collabora editör oturumu (DOCX dosyaları, iframe URL + WOPI token).</summary>
    [HttpGet("{id}/editor-lock-status")]
    [ProducesResponseType(typeof(DocumentEditorLockStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetEditorLockStatus(string id) =>
        Ok(_resourceEditor.GetEditorLockStatus(id));

    /// <summary>Collabora editör oturumu (DOCX dosyaları, iframe URL + WOPI token).</summary>
    [HttpGet("{id}/editor-session")]
    [ProducesResponseType(typeof(ResourceEditorSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEditorSession(
        string id,
        [FromQuery] bool? readOnly,
        [FromQuery] bool bypassLock = false,
        [FromQuery] string? postMessageOrigin = null,
        CancellationToken ct = default) =>
        Ok(await _resourceEditor.CreateEditorSessionAsync(id, readOnly, bypassLock, postMessageOrigin, ct));

    /// <summary>Yönetilen Office dosyası (DOCX/XLSX/PPTX) sürüm geçmişi (içerik hariç, en yeni önce).</summary>
    [HttpGet("{id}/versions")]
    [ProducesResponseType(typeof(IReadOnlyList<MarkdownVersionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFileVersions(string id, CancellationToken ct) =>
        Ok(await _resources.GetFileVersionsAsync(id, ct));

    /// <summary>Belirli bir Office sürümünü indirir.</summary>
    [HttpGet("{id}/versions/{versionNumber:int}/download")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadFileVersion(string id, int versionNumber, CancellationToken ct)
    {
        var (bytes, fileName) = await _resources.GetFileVersionContentAsync(id, versionNumber, ct);
        var ext = Path.GetExtension(fileName);
        var contentType = ManagedOfficeProfiles.TryResolve(ext, null, out var profile)
            ? profile.MimeType
            : "application/octet-stream";
        return File(bytes, contentType, fileName);
    }

    /// <summary>Belirli bir Office sürümünü salt okunur Collabora oturumunda açar.</summary>
    [HttpGet("{id}/versions/{versionNumber:int}/preview-session")]
    [ProducesResponseType(typeof(ResourceEditorSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetVersionPreviewSession(string id, int versionNumber, CancellationToken ct) =>
        Ok(await _resourceEditor.CreateVersionPreviewSessionAsync(id, versionNumber, ct));

    /// <summary>Eski bir Office sürümünü yeni sürüm olarak geri yükler.</summary>
    [HttpPost("{id}/versions/{versionNumber:int}/restore")]
    [ProducesResponseType(typeof(ResourceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RestoreFileVersion(string id, int versionNumber, CancellationToken ct) =>
        Ok(await _resources.RestoreFileVersionAsync(id, versionNumber, ct));

    /// <summary>Belirli bir Office sürümünün değişiklik notunu günceller.</summary>
    [HttpPatch("{id}/versions/{versionNumber:int}")]
    [ProducesResponseType(typeof(MarkdownVersionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateFileVersionChangeNote(
        string id,
        int versionNumber,
        [FromBody] UpdateFileVersionChangeNoteRequest request,
        CancellationToken ct) =>
        Ok(await _resources.UpdateFileVersionChangeNoteAsync(id, versionNumber, request, ct));

    /// <summary>
    /// Yüklenen dosya için metadata kaydı oluşturur. UI dönen <c>id</c> ile DG
    /// <c>POST /data/api/v1/files/upload</c> (datasetName=dm_resources, fieldName=file, recordId=id)
    /// çağrısı yaparak binary'yi MinIO'ya yükler.
    /// </summary>
    [HttpPost("file")]
    [ProducesResponseType(typeof(ResourceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateFileResource([FromBody] CreateFileResourceRequest request, CancellationToken ct)
    {
        var created = await _resources.CreateFileResourceAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Native DOCX döküman oluşturur (<c>origin=native</c>, antet uygulanır).</summary>
    [HttpPost("documents")]
    [ProducesResponseType(typeof(ResourceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> CreateNativeDocument([FromBody] CreateNativeDocumentRequest request, CancellationToken ct) =>
        CreateNativeDocumentCoreAsync(request, ct);

    /// <summary><c>POST documents</c> ile aynı — roadmap alias.</summary>
    [HttpPost("documents/native")]
    [ProducesResponseType(typeof(ResourceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> CreateNativeDocumentAlias([FromBody] CreateNativeDocumentRequest request, CancellationToken ct) =>
        CreateNativeDocumentCoreAsync(request, ct);

    private async Task<IActionResult> CreateNativeDocumentCoreAsync(CreateNativeDocumentRequest request, CancellationToken ct)
    {
        var created = await _resources.CreateNativeDocumentAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Boş native XLSX (sheet) oluşturur (<c>origin=native</c>).</summary>
    [HttpPost("sheets/native")]
    [ProducesResponseType(typeof(ResourceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateNativeSheet([FromBody] CreateNativeOfficeRequest request, CancellationToken ct)
    {
        var created = await _resources.CreateNativeSheetAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Boş native PPTX (sunum) oluşturur (<c>origin=native</c>).</summary>
    [HttpPost("presentations/native")]
    [ProducesResponseType(typeof(ResourceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateNativePresentation([FromBody] CreateNativeOfficeRequest request, CancellationToken ct)
    {
        var created = await _resources.CreateNativePresentationAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Yüklenen DOCX dosyasını PDF'e dönüştürerek önizleme sağlar (Gotenberg).</summary>
    [HttpGet("{id}/preview/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetFilePreviewPdf(string id, CancellationToken ct)
    {
        var (pdfBytes, fileName) = await _resources.GetFilePreviewPdfAsync(id, ct);
        return File(pdfBytes, "application/pdf", fileName);
    }

    /// <summary>Yönetilen DOCX / XLSX / PPTX kaynağını PDF olarak dışa aktarır (Gotenberg).</summary>
    [HttpGet("{id}/export/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetFileExportPdf(string id, CancellationToken ct)
    {
        var (pdfBytes, fileName) = await _resources.GetFileExportPdfAsync(id, ct);
        return File(pdfBytes, "application/pdf", fileName);
    }

    // ----- Grup bazlı klasör yetkilendirmesi + miras -----

    /// <summary>Klasörün yetki yönetim görünümü: miras durumu + grup matrisi + etkin yetki.</summary>
    [HttpGet("{id}/permissions")]
    [ProducesResponseType(typeof(FolderPermissionsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPermissions(string id, CancellationToken ct) =>
        Ok(await _permissions.GetFolderPermissionsAsync(id, ct));

    /// <summary>Anchor (mirası kırık) klasörde grup yetki matrisini değiştirir (tam değişim).</summary>
    [HttpPut("{id}/permissions")]
    [ProducesResponseType(typeof(FolderPermissionsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPermissions(string id, [FromBody] SetFolderPermissionsRequest request, CancellationToken ct) =>
        Ok(await _permissions.SetFolderPermissionsAsync(id, request, ct));

    /// <summary>Klasörün yetki mirasını kırar (üst anchor'ın ACL'ini kopyalar).</summary>
    [HttpPost("{id}/permissions/break-inheritance")]
    [ProducesResponseType(typeof(FolderPermissionsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BreakInheritance(string id, CancellationToken ct) =>
        Ok(await _permissions.BreakInheritanceAsync(id, ct));

    /// <summary>Klasörün kendi ACL'ini silip yetki mirasını geri yükler.</summary>
    [HttpPost("{id}/permissions/restore-inheritance")]
    [ProducesResponseType(typeof(FolderPermissionsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RestoreInheritance(string id, CancellationToken ct) =>
        Ok(await _permissions.RestoreInheritanceAsync(id, ct));
}
