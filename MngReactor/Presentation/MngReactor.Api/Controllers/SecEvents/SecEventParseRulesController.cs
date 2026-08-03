using Asp.Versioning;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Contracts.SecEvents;

using MngReactor.Application.Services.SecEvents;

namespace MngReactor.Api.Controllers.SecEvents;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/sec-events/parse-rules")]
[ApiController]
[Authorize]
public sealed class SecEventParseRulesController : ControllerBase
{
    private readonly ISecEventParseRuleCatalogService _catalog;
    private readonly ISecEventWindowsParseSampleService _windowsSamples;
    private readonly ISecEventLinuxParseSampleService _linuxSamples;

    public SecEventParseRulesController(
        ISecEventParseRuleCatalogService catalog,
        ISecEventWindowsParseSampleService windowsSamples,
        ISecEventLinuxParseSampleService linuxSamples)
    {
        _catalog = catalog;
        _windowsSamples = windowsSamples;
        _linuxSamples = linuxSamples;
    }

    [HttpGet("manage")]
    [ProducesResponseType(typeof(SecEventParseRuleManageListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SecEventParseRuleManageListResponse>> ListManaged(
        CancellationToken cancellationToken)
    {
        var domain = await GetDomainAsync();
        if (string.IsNullOrEmpty(domain))
            return Unauthorized();

        return Ok(await _catalog.ListManagedAsync(domain, cancellationToken));
    }

    [HttpGet("manage/{ruleId}")]
    [ProducesResponseType(typeof(SecEventParseRuleManageItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SecEventParseRuleManageItemDto>> GetManaged(
        string ruleId,
        CancellationToken cancellationToken)
    {
        var domain = await GetDomainAsync();
        if (string.IsNullOrEmpty(domain))
            return Unauthorized();

        var item = await _catalog.GetManagedAsync(domain, ruleId, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    [ProducesResponseType(typeof(SecEventParseRuleManageItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SecEventParseRuleManageItemDto>> Create(
        [FromBody] SecEventParseRuleUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var domain = await GetDomainAsync();
        if (string.IsNullOrEmpty(domain))
            return Unauthorized();

        try
        {
            return Ok(await _catalog.CreateAsync(domain, request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = "validation_failed", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = "conflict", message = ex.Message });
        }
    }

    [HttpPut("{ruleId}")]
    [ProducesResponseType(typeof(SecEventParseRuleManageItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SecEventParseRuleManageItemDto>> Update(
        string ruleId,
        [FromBody] SecEventParseRuleUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var domain = await GetDomainAsync();
        if (string.IsNullOrEmpty(domain))
            return Unauthorized();

        try
        {
            return Ok(await _catalog.UpdateAsync(domain, ruleId, request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = "validation_failed", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                return NotFound(new { error = "not_found", message = ex.Message });
            return BadRequest(new { error = "conflict", message = ex.Message });
        }
    }

    [HttpDelete("{ruleId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string ruleId, CancellationToken cancellationToken)
    {
        var domain = await GetDomainAsync();
        if (string.IsNullOrEmpty(domain))
            return Unauthorized();

        try
        {
            await _catalog.DeleteAsync(domain, ruleId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                return NotFound(new { error = "not_found", message = ex.Message });
            return BadRequest(new { error = "conflict", message = ex.Message });
        }
    }

    [HttpPost("publish")]
    [ProducesResponseType(typeof(SecEventParseRulePublishedResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SecEventParseRulePublishedResponse>> Publish(
        CancellationToken cancellationToken)
    {
        var domain = await GetDomainAsync();
        if (string.IsNullOrEmpty(domain))
            return Unauthorized();

        return Ok(await _catalog.PublishAsync(domain, cancellationToken));
    }

    [HttpGet("published")]
    [ProducesResponseType(typeof(SecEventParseRulePublishedResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SecEventParseRulePublishedResponse>> GetPublished(
        CancellationToken cancellationToken)
    {
        var domain = await GetDomainAsync();
        if (string.IsNullOrEmpty(domain))
            return Unauthorized();

        return Ok(await _catalog.GetPublishedAsync(domain, cancellationToken));
    }

    [HttpPost("preview")]
    [ProducesResponseType(typeof(SecEventParseRulePreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SecEventParseRulePreviewResponse>> Preview(
        [FromBody] SecEventParseRulePreviewRequest request,
        CancellationToken cancellationToken)
    {
        var domain = await GetDomainAsync();
        if (string.IsNullOrEmpty(domain))
            return Unauthorized();

        try
        {
            return Ok(await _catalog.PreviewAsync(domain, request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = "validation_failed", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = "conflict", message = ex.Message });
        }
    }

    /// <summary>Canonical target-field catalog (core + domain custom.*) for parse wizard + smart query.</summary>
    [HttpGet("target-fields")]
    [ProducesResponseType(typeof(SecEventTargetFieldCatalogResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SecEventTargetFieldCatalogResponse>> GetTargetFields(
        CancellationToken cancellationToken)
    {
        var domain = await GetDomainAsync();
        if (string.IsNullOrEmpty(domain))
            return Unauthorized();

        return Ok(await _catalog.GetTargetFieldsAsync(domain, cancellationToken));
    }

    [HttpPost("target-fields/custom")]
    [ProducesResponseType(typeof(SecEventTargetFieldDefinition), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SecEventTargetFieldDefinition>> UpsertCustomField(
        [FromBody] SecEventCustomFieldUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var domain = await GetDomainAsync();
        if (string.IsNullOrEmpty(domain))
            return Unauthorized();

        try
        {
            return Ok(await _catalog.UpsertCustomFieldAsync(domain, request, cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = "validation_failed", message = ex.Message });
        }
    }

    [HttpDelete("target-fields/custom/{*name}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCustomField(string name, CancellationToken cancellationToken)
    {
        var domain = await GetDomainAsync();
        if (string.IsNullOrEmpty(domain))
            return Unauthorized();

        try
        {
            await _catalog.DeleteCustomFieldAsync(domain, name, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = "validation_failed", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
                return NotFound(new { error = "not_found", message = ex.Message });
            return BadRequest(new { error = "conflict", message = ex.Message });
        }
    }

    /// <summary>Latest Windows Event Log samples for parse-rule wizard (OpenSearch fields.eventData).</summary>
    [HttpGet("~/api/v{version:apiVersion}/sec-events/parse-samples/windows")]
    [ProducesResponseType(typeof(SecEventWindowsParseSampleResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SecEventWindowsParseSampleResponse>> GetWindowsSamples(
        [FromQuery] string? channel,
        [FromQuery] int? eventId,
        [FromQuery] string? host,
        [FromQuery] int limit = 1,
        [FromQuery] int hours = 168,
        CancellationToken cancellationToken = default)
    {
        var domain = await GetDomainAsync();
        if (string.IsNullOrEmpty(domain))
            return Unauthorized();

        var result = await _windowsSamples.GetSamplesAsync(
            domain,
            new SecEventWindowsParseSampleRequest
            {
                Channel = channel,
                EventId = eventId,
                Host = host,
                Limit = limit,
                Hours = hours
            },
            cancellationToken);
        return Ok(result);
    }

    /// <summary>Latest linux-journal samples for parse-rule wizard (MESSAGE / package).</summary>
    [HttpGet("~/api/v{version:apiVersion}/sec-events/parse-samples/linux")]
    [ProducesResponseType(typeof(SecEventLinuxParseSampleResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SecEventLinuxParseSampleResponse>> GetLinuxSamples(
        [FromQuery] string? package,
        [FromQuery] string? query,
        [FromQuery] string? host,
        [FromQuery] int limit = 1,
        [FromQuery] int hours = 168,
        CancellationToken cancellationToken = default)
    {
        var domain = await GetDomainAsync();
        if (string.IsNullOrEmpty(domain))
            return Unauthorized();

        var result = await _linuxSamples.GetSamplesAsync(
            domain,
            new SecEventLinuxParseSampleRequest
            {
                Package = package,
                Query = query,
                Host = host,
                Limit = limit,
                Hours = hours
            },
            cancellationToken);
        return Ok(result);
    }

    private async Task<string?> GetDomainAsync()
    {
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        var tokenValue = authHeader != null && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authHeader["Bearer ".Length..]
            : null;
        if (string.IsNullOrEmpty(tokenValue))
        {
            var auth = await HttpContext.AuthenticateAsync();
            tokenValue = auth.Properties?.Items?.FirstOrDefault(x => x.Key == ".Token.access_token").Value;
        }

        if (string.IsNullOrEmpty(tokenValue))
            return null;

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(tokenValue);
        return token.Claims.FirstOrDefault(c => c.Type == "domain_name" || c.Type == "domain")?.Value;
    }
}
