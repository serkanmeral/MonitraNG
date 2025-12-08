using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngDataGateway.Application.Services;

namespace MngDataGateway.Api.Controllers
{
    /// <summary>
    /// Health check controller for monitoring application health
    /// </summary>
    [ApiController]
    [Route("api/health")]
    [AllowAnonymous] // Health check should be accessible without authentication
    public class HealthController : ControllerBase
    {
        private readonly ILogger<HealthController> _logger;
        private readonly IHealthCheckService _healthCheckService;

        public HealthController(
            ILogger<HealthController> logger,
            IHealthCheckService healthCheckService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _healthCheckService = healthCheckService ?? throw new ArgumentNullException(nameof(healthCheckService));
        }

        /// <summary>
        /// Get application health status
        /// </summary>
        /// <returns>Health status with component checks</returns>
        [HttpGet]
        [ProducesResponseType(typeof(HealthCheckResult), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(HealthCheckResult), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> GetHealth()
        {
            try
            {
                var result = await _healthCheckService.CheckHealthAsync();

                // Return appropriate status code based on health
                var statusCode = result.Status switch
                {
                    "healthy" => StatusCodes.Status200OK,
                    "degraded" => StatusCodes.Status200OK, // Still OK but with warnings
                    "unhealthy" => StatusCodes.Status503ServiceUnavailable,
                    _ => StatusCodes.Status200OK
                };

                _logger.LogInformation(
                    "Health check completed: {Status} (MongoDB: {MongoStatus}, RabbitMQ: {RabbitStatus}, Disk: {DiskStatus})",
                    result.Status,
                    result.Checks.MongoDB.Status,
                    result.Checks.RabbitMQ.Status,
                    result.Checks.Disk.Status);

                return StatusCode(statusCode, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed with exception");

                var errorResult = new HealthCheckResult
                {
                    Status = "unhealthy",
                    Timestamp = DateTime.UtcNow,
                    Checks = new HealthCheckDetails
                    {
                        MongoDB = new ComponentHealth { Status = "unknown", Message = "Health check failed" },
                        RabbitMQ = new ComponentHealth { Status = "unknown", Message = "Health check failed" },
                        Disk = new ComponentHealth { Status = "unknown", Message = "Health check failed" }
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
        public async Task<IActionResult> Ready()
        {
            var result = await _healthCheckService.CheckHealthAsync();

            // For readiness, we need MongoDB and RabbitMQ to be healthy
            var isReady = result.Checks.MongoDB.Status == "healthy" &&
                         result.Checks.RabbitMQ.Status == "healthy";

            if (isReady)
            {
                return Ok(new
                {
                    status = "ready",
                    timestamp = DateTime.UtcNow,
                    checks = new
                    {
                        mongodb = result.Checks.MongoDB.Status,
                        rabbitmq = result.Checks.RabbitMQ.Status
                    }
                });
            }

            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "not ready",
                timestamp = DateTime.UtcNow,
                checks = new
                {
                    mongodb = result.Checks.MongoDB.Status,
                    rabbitmq = result.Checks.RabbitMQ.Status
                }
            });
        }
    }
}

