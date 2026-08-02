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

    [HttpPost("scan")]
    [ProducesResponseType(typeof(DiscoveryScanStartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DiscoveryScanStartResponse>> StartScan(
        [FromBody] DiscoveryScanStartRequest? request,
        [FromQuery] string? domainId = null,
        CancellationToken ct = default)
    {
        var body = request ?? new DiscoveryScanStartRequest();
        if (string.IsNullOrWhiteSpace(body.DomainId))
            body.DomainId = domainId;
        if (string.IsNullOrWhiteSpace(body.DomainId))
            body.DomainId = Request.Headers["X-Domain-Name"].FirstOrDefault();

        try
        {
            var result = await discovery.StartScanAsync(body, ct);
            if (result.Status == "error")
            {
                if (result.Error?.Contains("already", StringComparison.OrdinalIgnoreCase) == true)
                    return Conflict(result);
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new DiscoveryScanStartResponse
            {
                Status = "error",
                Error = ex.Message
            });
        }
    }

    [HttpGet("scan/{runId}")]
    [ProducesResponseType(typeof(DiscoveryScanStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DiscoveryScanStatusResponse>> GetScan(
        [FromRoute] string runId,
        [FromQuery] string domainId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(domainId))
            return BadRequest(new { error = "domainId is required." });

        try
        {
            var result = await discovery.GetScanAsync(domainId, runId, ct);
            if (result is null)
                return NotFound(new { error = "Scan job not found." });
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("scan/{runId}/cancel")]
    [ProducesResponseType(typeof(DiscoveryScanStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DiscoveryScanStatusResponse>> CancelScan(
        [FromRoute] string runId,
        [FromBody] DiscoveryScanStartRequest? request,
        [FromQuery] string? domainId = null,
        CancellationToken ct = default)
    {
        var id = domainId ?? request?.DomainId;
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest(new { error = "domainId is required." });

        try
        {
            var result = await discovery.CancelScanAsync(id, runId, ct);
            if (result is null)
                return NotFound(new { error = "Scan job not found." });
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("prefixes")]
    [ProducesResponseType(typeof(DiscoveryPrefixesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DiscoveryPrefixesResponse>> GetPrefixes(
        [FromQuery] string domainId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(domainId))
            return BadRequest(new { error = "domainId is required." });

        try
        {
            return Ok(await discovery.GetPrefixesAsync(domainId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPut("prefixes")]
    [ProducesResponseType(typeof(DiscoveryPrefixesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DiscoveryPrefixesResponse>> PutPrefixes(
        [FromBody] DiscoveryPrefixesPutRequest? request,
        [FromQuery] string? domainId = null,
        CancellationToken ct = default)
    {
        var id = domainId ?? Request.Headers["X-Domain-Name"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest(new { error = "domainId is required." });

        try
        {
            return Ok(await discovery.PutPrefixesAsync(id, request ?? new DiscoveryPrefixesPutRequest(), ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new DiscoveryPrefixesResponse
            {
                DomainId = id,
                Error = ex.Message
            });
        }
    }

    [HttpPost("hosts/clear")]
    [ProducesResponseType(typeof(DiscoveryClearResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DiscoveryClearResponse>> ClearHosts(
        [FromBody] DiscoveryClearRequest? request,
        [FromQuery] string? domainId = null,
        CancellationToken ct = default)
    {
        var body = request ?? new DiscoveryClearRequest();
        if (string.IsNullOrWhiteSpace(body.DomainId))
            body.DomainId = domainId;
        if (string.IsNullOrWhiteSpace(body.DomainId))
            body.DomainId = Request.Headers["X-Domain-Name"].FirstOrDefault();

        try
        {
            var result = await discovery.ClearHostsAsync(body, ct);
            if (result.Status == "error")
            {
                if (result.Error?.Contains("already", StringComparison.OrdinalIgnoreCase) == true)
                    return Conflict(result);
                return BadRequest(result);
            }

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new DiscoveryClearResponse
            {
                Status = "error",
                Error = ex.Message
            });
        }
    }
}
