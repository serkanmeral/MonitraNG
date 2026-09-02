using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using MngLogCollector.Application.Abstractions.AgentPackages;
using MngLogCollector.Application.Configuration;
using MngLogCollector.Application.Contracts.AgentPackages;

namespace MngLogCollector.Api.Controllers;

/// <summary>
/// Public IT download of MngLogs agent installers (no ingest API key).
/// Files live on a host volume; this collector is the same URL agents will use.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/agent/packages")]
public sealed class AgentPackagesController(
    IAgentPackageCatalog catalog,
    IOptions<MngLogCollectorSettings> settings) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(AgentPackageCatalogResponse), StatusCodes.Status200OK)]
    public ActionResult<AgentPackageCatalogResponse> List() =>
        Ok(catalog.GetCatalog(ResolveRequestBaseUrl()));

    [HttpGet("{id}")]
    [HttpHead("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Download([FromRoute] string id)
    {
        var file = catalog.GetFile(id);
        if (file is null)
            return NotFound(new { error = "Agent package not found." });

        return PhysicalFile(
            file.AbsolutePath,
            file.ContentType,
            file.FileName,
            enableRangeProcessing: true);
    }

    private string ResolveRequestBaseUrl()
    {
        var configured = settings.Value.AgentPackages?.PublicBaseUrl;
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.Trim().TrimEnd('/');

        var proto = Request.Headers["X-Forwarded-Proto"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(proto))
            proto = Request.Scheme;
        return $"{proto}://{Request.Host.Value}".TrimEnd('/');
    }
}
