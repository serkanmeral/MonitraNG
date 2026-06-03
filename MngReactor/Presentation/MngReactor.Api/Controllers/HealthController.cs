using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MngReactor.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/health")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;
    private readonly HealthCheckService _healthCheckService;

    public HealthController(ILogger<HealthController> logger, HealthCheckService healthCheckService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _healthCheckService = healthCheckService ?? throw new ArgumentNullException(nameof(healthCheckService));
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetHealth(CancellationToken cancellationToken)
    {
        try
        {
            var report = await _healthCheckService.CheckHealthAsync(cancellationToken);
            var checks = new Dictionary<string, object>();
            foreach (var kv in report.Entries)
                checks[kv.Key] = FormatCheck(kv.Value);

            var overallStatus = report.Status == HealthStatus.Healthy ? "healthy" : "unhealthy";
            var result = new
            {
                Status = overallStatus,
                Timestamp = DateTime.UtcNow,
                Checks = checks
            };

            _logger.LogDebug("Health check completed: {Status}", overallStatus);
            return report.Status == HealthStatus.Healthy
                ? Ok(result)
                : StatusCode(StatusCodes.Status503ServiceUnavailable, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                Status = "unhealthy",
                Timestamp = DateTime.UtcNow,
                Checks = new Dictionary<string, object> { ["error"] = new { Status = "unhealthy", Message = ex.Message } }
            });
        }
    }

    private static object FormatCheck(HealthReportEntry? entry)
    {
        if (entry is not { } e)
            return new { Status = "unknown", Message = "Check not registered" };
        var status = e.Status switch { HealthStatus.Healthy => "healthy", HealthStatus.Degraded => "degraded", _ => "unhealthy" };
        var msg = e.Status == HealthStatus.Healthy ? "Connected" : (e.Description ?? e.Exception?.Message ?? "Unhealthy");
        return new { Status = status, Message = msg };
    }

    [HttpGet("live")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Live()
    {
        return Ok(new { status = "alive", timestamp = DateTime.UtcNow });
    }

    [HttpGet("ready")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public IActionResult Ready()
    {
        return Ok(new { status = "ready", timestamp = DateTime.UtcNow });
    }
}
