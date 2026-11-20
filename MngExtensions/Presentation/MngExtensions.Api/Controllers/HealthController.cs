using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngExtensions.Application.Pdks;

namespace MngExtensions.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/health")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private readonly IHealthCheckService _health;

    public HealthController(IHealthCheckService health)
    {
        _health = health;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var result = await _health.CheckHealthAsync(cancellationToken);
        var status = result.Status == "healthy" ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable;
        return StatusCode(status, result);
    }

    [HttpGet("live")]
    public IActionResult Live() => Ok(new { status = "alive" });
}
