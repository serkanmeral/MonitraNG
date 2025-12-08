using System.Threading.Tasks;

namespace MngDataGateway.Application.Services
{
    /// <summary>
    /// Health check service for monitoring application health
    /// </summary>
    public interface IHealthCheckService
    {
        /// <summary>
        /// Perform comprehensive health check
        /// </summary>
        Task<HealthCheckResult> CheckHealthAsync();
    }

    /// <summary>
    /// Health check result
    /// </summary>
    public class HealthCheckResult
    {
        public string Status { get; set; } = "healthy"; // healthy, degraded, unhealthy
        public System.DateTime Timestamp { get; set; } = System.DateTime.UtcNow;
        public HealthCheckDetails Checks { get; set; } = new();
    }

    /// <summary>
    /// Health check details for each component
    /// </summary>
    public class HealthCheckDetails
    {
        public ComponentHealth MongoDB { get; set; } = new();
        public ComponentHealth RabbitMQ { get; set; } = new();
        public ComponentHealth Disk { get; set; } = new();
    }

    /// <summary>
    /// Individual component health status
    /// </summary>
    public class ComponentHealth
    {
        public string Status { get; set; } = "healthy"; // healthy, degraded, unhealthy
        public long? ResponseTimeMs { get; set; }
        public string? Message { get; set; }
    }
}

