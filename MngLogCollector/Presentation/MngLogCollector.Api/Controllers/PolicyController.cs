using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using MngLogCollector.Api.Filters;
using MngLogCollector.Application.Abstractions.Policy;
using MngLogCollector.Application.Contracts.Policy;

namespace MngLogCollector.Api.Controllers;

/// <summary>Policy pull endpoints for field agents (same API key as ingest).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/policy")]
[IngestApiKey]
public sealed class PolicyController(IEventLogPackageCatalogService catalog) : ControllerBase
{
    /// <summary>Event Log package catalog (server source of truth for agent merge).</summary>
    [HttpGet("eventlog-packages")]
    [ProducesResponseType(typeof(EventLogPackageCatalogResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<EventLogPackageCatalogResponse> GetEventLogPackages()
    {
        var response = catalog.GetCatalog();
        var etag = $"\"{response.Version}\"";
        Response.Headers.ETag = etag;

        var incoming = Request.Headers.IfNoneMatch.ToString();
        if (!string.IsNullOrWhiteSpace(incoming) &&
            string.Equals(incoming.Trim(), etag, StringComparison.Ordinal))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        return Ok(response);
    }
}
