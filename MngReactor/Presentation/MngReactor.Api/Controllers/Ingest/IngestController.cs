using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngReactor.Application.Features.Commands.Ingest;

namespace MngReactor.Api.Controllers.Ingest;

[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize]
public class IngestController : ControllerBase
{
    private readonly IMediator _mediator;

    public IngestController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Engine'den metrik batch'lerini alır. MongoDB Time Series'e yazar, RabbitMQ'ya publish eder, lastSeenAt günceller.
    /// </summary>
    [HttpPost("metrics")]
    [ProducesResponseType(typeof(IngestMetricsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IngestMetricsResponse>> IngestMetrics([FromBody] IngestMetricsRequest request, CancellationToken cancellationToken)
    {
        var (domain, accessToken) = await GetDomainAndTokenAsync();
        if (string.IsNullOrEmpty(domain))
            return Unauthorized();

        if (request?.Batches == null || request.Batches.Count == 0)
            return BadRequest(new { error = "batches_required", message = "At least one batch is required" });

        var response = await _mediator.Send(new IngestMetricsCommand(request, domain, accessToken), cancellationToken);
        return Ok(response);
    }

    private async Task<(string? domain, string? accessToken)> GetDomainAndTokenAsync()
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
        if (string.IsNullOrEmpty(tokenValue)) return (null, null);

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(tokenValue);
        var domain = token.Claims.FirstOrDefault(c => c.Type == "domain_name" || c.Type == "domain")?.Value;
        return (domain, tokenValue);
    }
}
