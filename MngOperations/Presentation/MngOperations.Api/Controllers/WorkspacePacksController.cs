using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngOperations.Application.WorkspacePacks;

namespace MngOperations.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/workspace-packs")]
[Authorize]
public sealed class WorkspacePacksController : ControllerBase
{
    [HttpGet]
    public IActionResult List()
    {
        var items = WorkspacePackCatalog.All.Select(WorkspacePackCatalog.ToSummary).ToList();
        return Ok(new { items });
    }

    [HttpGet("{code}")]
    public IActionResult Get(string code)
    {
        var pack = WorkspacePackCatalog.Find(code);
        if (pack is null) return NotFound();
        return Ok(pack);
    }
}
