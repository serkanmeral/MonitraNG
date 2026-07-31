using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using MngLogCollector.Api.Filters;
using MngLogCollector.Application.Abstractions.Discovery;
using MngLogCollector.Application.Contracts.Discovery;

namespace MngLogCollector.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/discovery")]
[IngestApiKey]
public sealed class DiscoveryController(IDiscoveryService discovery) : ControllerBase
{
    [HttpGet("hosts")]
    [ProducesResponseType(typeof(DiscoveryHostListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DiscoveryHostListResponse>> ListHosts(
        [FromQuery] string domainId,
        [FromQuery] string? q = null,
        [FromQuery] string? source = null,
        [FromQuery] int limit = 500,
        [FromQuery] int offset = 0,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(domainId))
            return BadRequest(new { error = "domainId is required." });

        try
        {
            var result = await discovery.ListHostsAsync(domainId, q, source, limit, offset, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(DiscoverySummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DiscoverySummaryResponse>> Summary(
        [FromQuery] string domainId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(domainId))
            return BadRequest(new { error = "domainId is required." });

        try
        {
            return Ok(await discovery.GetSummaryAsync(domainId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("sync")]
    [ProducesResponseType(typeof(DiscoverySyncResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DiscoverySyncResponse>> Sync(
        [FromBody] DiscoverySyncRequest? request,
        CancellationToken ct = default)
    {
        var body = request ?? new DiscoverySyncRequest();
        var result = await discovery.SyncAsync(body, ct);
        if (result.Status == "error" && result.Domains.Count == 0)
            return BadRequest(result);
        return Ok(result);
    }
}
