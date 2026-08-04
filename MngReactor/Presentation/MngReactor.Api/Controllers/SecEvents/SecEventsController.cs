using Asp.Versioning;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Models.SecEvents;
using MngReactor.Application.Services.SecEvents;

namespace MngReactor.Api.Controllers.SecEvents;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/sec-events")]
[ApiController]
[Authorize]
public sealed class SecEventsController : ControllerBase
{
    private readonly ISecEventsRepository _repository;

    public SecEventsController(ISecEventsRepository repository)
    {
        _repository = repository;
    }

    /// <summary>sec_events koleksiyonunda filtreli arama (SIEM olay gezgini).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(SecEventQueryResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SecEventQueryResult>> Query(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? sourceType,
        [FromQuery] string? sourceProduct,
        [FromQuery] string? eventAction,
        [FromQuery] string? eventActions,
        [FromQuery] string? eventActionPrefix,
        [FromQuery] string? eventOutcome,
        [FromQuery] string? srcIp,
        [FromQuery] string? dstIp,
        [FromQuery] string? dstPort,
        [FromQuery] string? actorUser,
        [FromQuery] string? sourceHost,
        [FromQuery] string? sourceHosts,
        [FromQuery] string? eventCode,
        [FromQuery] string? eventCodes,
        [FromQuery] string? search,
        [FromQuery] string? fieldFilters,
        [FromQuery] bool excludeUnknown = true,
        [FromQuery] int skip = 0,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var domain = await GetDomainAsync();
        if (string.IsNullOrEmpty(domain))
            return Unauthorized();

        var effectiveFrom = from;
        if (!effectiveFrom.HasValue && !to.HasValue)
            effectiveFrom = DateTime.UtcNow.AddHours(-24);

        var result = await _repository.QueryAsync(
            domain,
            new SecEventQueryFilter
            {
                From = effectiveFrom,
                To = to,
                SourceType = sourceType,
                SourceProduct = sourceProduct,
                EventAction = eventAction,
                EventActions = eventActions,
                EventActionPrefix = eventActionPrefix,
                EventOutcome = eventOutcome,
                SrcIp = srcIp,
                DstIp = dstIp,
                DstPort = dstPort,
                ActorUser = actorUser,
                SourceHost = sourceHost,
                SourceHosts = sourceHosts,
                EventCode = eventCode,
                EventCodes = eventCodes,
                Search = search,
                FieldFilters = SecEventFieldQueryHelper.ParseFieldFiltersJson(fieldFilters),
                ExcludeUnknown = excludeUnknown,
                Skip = skip,
                Limit = limit
            },
            cancellationToken);

        return Ok(result);
    }

    /// <summary>SIEM güvenlik paneli — tek aggregation ile 24s özet (saatlik + aksiyon dağılımı).</summary>
    [HttpGet("dashboard-summary")]
    [ProducesResponseType(typeof(SecEventDashboardSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SecEventDashboardSummary>> DashboardSummary(
        [FromQuery] int rangeHours = 24,
        [FromQuery] bool excludeUnknown = true,
        CancellationToken cancellationToken = default)
    {
        var domain = await GetDomainAsync();
        if (string.IsNullOrEmpty(domain))
            return Unauthorized();

        var summary = await _repository.GetDashboardSummaryAsync(
            domain,
            new SecEventDashboardSummaryRequest
            {
                RangeHours = rangeHours,
                ExcludeUnknown = excludeUnknown,
            },
            cancellationToken);

        return Ok(summary);
    }

    /// <summary>Distinct source.type / product / host for filter scope comboboxes (live index).</summary>
    [HttpGet("scope-options")]
    [ProducesResponseType(typeof(SecEventScopeOptions), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SecEventScopeOptions>> ScopeOptions(
        [FromQuery] int rangeHours = 168,
        CancellationToken cancellationToken = default)
    {
        var domain = await GetDomainAsync();
        if (string.IsNullOrEmpty(domain))
            return Unauthorized();

        var options = await _repository.GetScopeOptionsAsync(domain, rangeHours, cancellationToken);
        return Ok(options);
    }

    /// <summary>
    /// Fetch one event by id via query string — safe for ids that contain '/'
    /// (e.g. Windows channel paths like "...LocalSessionManager/Operational:123:25").
    /// </summary>
    [HttpGet("by-id")]
    [ProducesResponseType(typeof(SecEventListItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SecEventListItem>> GetByIdQuery(
        [FromQuery] string id,
        CancellationToken cancellationToken)
    {
        var domain = await GetDomainAsync();
        if (string.IsNullOrEmpty(domain))
            return Unauthorized();
        if (string.IsNullOrWhiteSpace(id))
            return NotFound();

        var item = await _repository.GetByIdAsync(domain, id.Trim(), cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    /// <summary>Path-based get; catch-all so ids with '/' still bind.</summary>
    [HttpGet("{**id}")]
    [ProducesResponseType(typeof(SecEventListItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SecEventListItem>> GetById(string id, CancellationToken cancellationToken)
    {
        var domain = await GetDomainAsync();
        if (string.IsNullOrEmpty(domain))
            return Unauthorized();

        // ASP.NET may leave catch-all encoded; normalize once.
        var normalized = Uri.UnescapeDataString(id ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return NotFound();

        var item = await _repository.GetByIdAsync(domain, normalized, cancellationToken);
        return item is null ? NotFound() : Ok(item);
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
