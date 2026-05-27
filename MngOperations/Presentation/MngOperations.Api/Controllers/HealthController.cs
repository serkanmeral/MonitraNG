using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngOperations.Application.Interfaces;

namespace MngOperations.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/health")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private readonly IHealthCheckService _healthCheckService;

    public HealthController(IHealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var report = await _healthCheckService.GetHealthAsync(cancellationToken);
        var statusCode = report.Status switch
        {
            "unhealthy" => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status200OK
        };

        return StatusCode(statusCode, new
        {
            status = report.Status,
            timestamp = report.Timestamp,
            checks = report.Checks
        });
    }

    [HttpGet("live")]
    public IActionResult Live() => Ok(new { status = "alive", timestamp = DateTime.UtcNow });
}
