using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngOperations.Application.Interfaces;

namespace MngOperations.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/workspaces")]
[Authorize]
public sealed class WorkspacesController : ControllerBase
{
    private readonly IMetadataCacheAdminService _metadataCacheAdmin;

    public WorkspacesController(IMetadataCacheAdminService metadataCacheAdmin)
    {
        _metadataCacheAdmin = metadataCacheAdmin;
    }

    /// <summary>
    /// Workspace metadata önbelleğini düşürür; sonraki runtime istekleri DG'den taze okur.
    /// Form/board/alan tanımı DG'de güncellendikten sonra restart beklemeden kullanılır.
    /// </summary>
    [HttpPost("{workspaceId}/metadata-cache/reload")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReloadMetadataCache(string workspaceId, CancellationToken cancellationToken)
    {
        var result = await _metadataCacheAdmin.ReloadWorkspaceAsync(workspaceId, cancellationToken);
        return Ok(result);
    }
}
