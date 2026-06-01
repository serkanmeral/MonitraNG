using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngDocument.Application.Contracts.Resources;
using MngDocument.Application.Interfaces;

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

    public ResourcesController(IResourceService resources, IPermissionService permissions)
    {
        _resources = resources;
        _permissions = permissions;
    }

    /// <summary>Klasör ağacı (yalnızca klasörler, iç içe).</summary>
    [HttpGet("tree")]
    [ProducesResponseType(typeof(IReadOnlyList<TreeNodeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTree(CancellationToken ct) =>
        Ok(await _resources.GetTreeAsync(ct));

    /// <summary>Bir klasörün içeriği (klasör + markdown + dosya). parentId boşsa kök.</summary>
    [HttpGet("children")]
    [ProducesResponseType(typeof(ResourceListResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChildren([FromQuery] string? parentId, CancellationToken ct) =>
        Ok(await _resources.GetChildrenAsync(parentId, ct));

    /// <summary>Full-text arama (DG regex search: ad/başlık/açıklama + markdown içeriği).</summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(ResourceListResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] int skip = 0,
        [FromQuery] int limit = 50,
        CancellationToken ct = default) =>
        Ok(await _resources.SearchAsync(q, skip, limit, ct));

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

    [HttpPut("{id}/move")]
    [ProducesResponseType(typeof(ResourceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Move(string id, [FromBody] MoveResourceRequest request, CancellationToken ct) =>
        Ok(await _resources.MoveAsync(id, request, ct));

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
