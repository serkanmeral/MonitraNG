using Asp.Versioning;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Models.SecEvents;

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
        [FromQuery] string? eventAction,
        [FromQuery] string? srcIp,
        [FromQuery] string? actorUser,
        [FromQuery] string? search,
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
                EventAction = eventAction,
                SrcIp = srcIp,
                ActorUser = actorUser,
                Search = search,
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

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(SecEventListItem), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SecEventListItem>> GetById(string id, CancellationToken cancellationToken)
    {
        var domain = await GetDomainAsync();
        if (string.IsNullOrEmpty(domain))
            return Unauthorized();

        var item = await _repository.GetByIdAsync(domain, id, cancellationToken);
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
