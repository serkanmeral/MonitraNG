using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MngScheduler.Api.Controllers;

/// <summary>
/// Health check controller for monitoring application health
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/health")]
[AllowAnonymous] // Health check should be accessible without authentication
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;

    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get application health status
    /// </summary>
    /// <returns>Health status with component checks</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public IActionResult GetHealth()
    {
        try
        {
            // TODO: Implement health check service
            var result = new
            {
                Status = "healthy",
                Timestamp = DateTime.UtcNow,
                Checks = new
                {
                    MongoDB = new { Status = "healthy", Message = "Connected" },
                    RabbitMQ = new { Status = "healthy", Message = "Connected" },
                    Quartz = new { Status = "healthy", Message = "Running" }
                }
            };

            _logger.LogInformation("Health check completed: {Status}", result.Status);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed with exception");

            var errorResult = new
            {
                Status = "unhealthy",
                Timestamp = DateTime.UtcNow,
                Checks = new
                {
                    MongoDB = new { Status = "unknown", Message = "Health check failed" },
                    RabbitMQ = new { Status = "unknown", Message = "Health check failed" },
                    Quartz = new { Status = "unknown", Message = "Health check failed" }
                }
            };

            return StatusCode(StatusCodes.Status503ServiceUnavailable, errorResult);
        }
    }

    /// <summary>
    /// Simple liveness probe (Kubernetes/Docker)
    /// </summary>
    /// <returns>Simple alive status</returns>
    [HttpGet("live")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Live()
    {
        return Ok(new
        {
            status = "alive",
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Readiness probe (Kubernetes/Docker)
    /// Checks if application is ready to accept traffic
    /// </summary>
    /// <returns>Readiness status</returns>
    [HttpGet("ready")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public IActionResult Ready()
    {
        // TODO: Implement readiness check
        // For readiness, we need MongoDB to be healthy
        var isReady = true; // Placeholder

        if (isReady)
        {
            return Ok(new
            {
                status = "ready",
                timestamp = DateTime.UtcNow
            });
        }

        return StatusCode(StatusCodes.Status503ServiceUnavailable, new
        {
            status = "not ready",
            timestamp = DateTime.UtcNow
        });
    }
}
