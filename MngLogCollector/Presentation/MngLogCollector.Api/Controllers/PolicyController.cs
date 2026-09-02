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
public sealed class PolicyController(
    IEventLogPackageCatalogService catalog,
    IDlpPolicyCatalogService dlp) : ControllerBase
{
    public const string HostnameHeader = "X-MngLogs-Hostname";

    /// <summary>Event Log package catalog (server source of truth for agent merge).</summary>
    [HttpGet("eventlog-packages")]
    [ProducesResponseType(typeof(EventLogPackageCatalogResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<EventLogPackageCatalogResponse>> GetEventLogPackages(
        [FromQuery] string? hostname,
        CancellationToken ct)
    {
        var host = hostname;
        if (string.IsNullOrWhiteSpace(host))
            host = Request.Headers[HostnameHeader].FirstOrDefault();

        var response = await catalog.GetCatalogAsync(host, ct);
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

    /// <summary>Managed catalog list (includes isDefault / publish meta).</summary>
    [HttpGet("eventlog-packages/manage")]
    [ProducesResponseType(typeof(EventLogPackageManageListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<EventLogPackageManageListResponse>> ListManaged(CancellationToken ct) =>
        Ok(await catalog.ListManagedAsync(ct));

    [HttpGet("eventlog-packages/channels")]
    [ProducesResponseType(typeof(IReadOnlyList<EventLogChannelDictionaryDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<EventLogChannelDictionaryDto>> Channels() =>
        Ok(catalog.GetChannelDictionary());

    [HttpGet("eventlog-packages/presets")]
    [ProducesResponseType(typeof(IReadOnlyList<EventLogPackagePresetDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<EventLogPackagePresetDto>> Presets() =>
        Ok(catalog.GetPresets());

    [HttpGet("eventlog-packages/assignments/{hostname}")]
    [ProducesResponseType(typeof(EventLogHostAssignmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EventLogHostAssignmentDto>> GetAssignment(string hostname, CancellationToken ct)
    {
        try
        {
            return Ok(await catalog.GetAssignmentAsync(hostname, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("eventlog-packages/assignments/{hostname}")]
    [ProducesResponseType(typeof(EventLogHostAssignmentDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EventLogHostAssignmentDto>> PutAssignment(
        string hostname,
        [FromBody] EventLogHostAssignmentUpsertRequest request,
        CancellationToken ct)
    {
        try
        {
            return Ok(await catalog.UpsertAssignmentAsync(hostname, request, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("eventlog-packages/assignments/{hostname}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAssignment(string hostname, CancellationToken ct)
    {
        try
        {
            await catalog.DeleteAssignmentAsync(hostname, ct);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("eventlog-packages")]
    [ProducesResponseType(typeof(EventLogPackageManageItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EventLogPackageManageItemDto>> Create(
        [FromBody] EventLogPackageUpsertRequest request,
        CancellationToken ct)
    {
        try
        {
            var item = await catalog.CreateAsync(request, ct);
            return CreatedAtAction(nameof(ListManaged), new { version = "1.0" }, item);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPut("eventlog-packages/{name}")]
    [ProducesResponseType(typeof(EventLogPackageManageItemDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EventLogPackageManageItemDto>> Update(
        string name,
        [FromBody] EventLogPackageUpsertRequest request,
        CancellationToken ct)
    {
        try
        {
            return Ok(await catalog.UpdateAsync(name, request, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpDelete("eventlog-packages/{name}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(string name, CancellationToken ct)
    {
        try
        {
            await catalog.DeleteAsync(name, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    /// <summary>Publish catalog version (agents pick up on next periodic pull).</summary>
    [HttpPost("eventlog-packages/publish")]
    [ProducesResponseType(typeof(EventLogPackageCatalogResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<EventLogPackageCatalogResponse>> Publish(CancellationToken ct) =>
        Ok(await catalog.PublishAsync(ct));

    /// <summary>Published DLP policy for agents (ETag = version).</summary>
    [HttpGet("dlp")]
    [ProducesResponseType(typeof(DlpPolicyResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DlpPolicyResponse>> GetDlp(CancellationToken ct)
    {
        var response = await dlp.GetPublishedAsync(ct);
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

    [HttpGet("dlp/manage")]
    [ProducesResponseType(typeof(DlpPolicyManageResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DlpPolicyManageResponse>> GetDlpManage(CancellationToken ct) =>
        Ok(await dlp.GetManageAsync(ct));

    [HttpPut("dlp")]
    [ProducesResponseType(typeof(DlpPolicyManageResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DlpPolicyManageResponse>> PutDlp(
        [FromBody] DlpPolicyUpsertRequest request,
        CancellationToken ct)
    {
        try
        {
            return Ok(await dlp.UpsertDraftAsync(request, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("dlp/publish")]
    [ProducesResponseType(typeof(DlpPolicyResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DlpPolicyResponse>> PublishDlp(CancellationToken ct) =>
        Ok(await dlp.PublishAsync(ct));
}
