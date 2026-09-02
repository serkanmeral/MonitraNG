using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngDocument.Application.Contracts.Catalogs;
using MngDocument.Application.Interfaces;

namespace MngDocument.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[Authorize]
public sealed class CatalogsController : ControllerBase
{
    private readonly IResourceKindCatalog _kinds;
    private readonly IRelationTypeCatalog _relations;

    public CatalogsController(IResourceKindCatalog kinds, IRelationTypeCatalog relations)
    {
        _kinds = kinds;
        _relations = relations;
    }

    [HttpGet("resource-kinds")]
    [ProducesResponseType(typeof(CatalogListResult<ResourceKindDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListKinds([FromQuery] bool activeOnly = true, CancellationToken ct = default) =>
        Ok(await _kinds.ListAsync(activeOnly, ct));

    [HttpGet("relation-types")]
    [ProducesResponseType(typeof(CatalogListResult<RelationTypeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListRelations([FromQuery] bool activeOnly = true, CancellationToken ct = default) =>
        Ok(await _relations.ListAsync(activeOnly, ct));
}
