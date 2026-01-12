using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MngNotifier.Application.Services;

namespace MngNotifier.Persistence.Services
{
    /// <summary>
    /// Health check service implementation
    /// </summary>
    public class HealthCheckService : IHealthCheckService
    {
        private readonly ILogger<HealthCheckService> _logger;

        public HealthCheckService(
            ILogger<HealthCheckService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<HealthCheckResult> CheckHealthAsync()
        {
            var result = new HealthCheckResult
            {
                Timestamp = DateTime.UtcNow
            };

            // Check RabbitMQ (TODO: Implement when RabbitMQ service is available)
            result.Checks.RabbitMQ = CheckRabbitMQ();

            // Check Disk
            result.Checks.Disk = CheckDisk();

            // Determine overall status
            result.Status = DetermineOverallStatus(result.Checks);

            return await Task.FromResult(result);
        }

        private ComponentHealth CheckRabbitMQ()
        {
            var health = new ComponentHealth();

            // TODO: Implement RabbitMQ health check when IRabbitMqService is available
            // For now, mark as not implemented
            health.Status = "degraded";
            health.Message = "RabbitMQ health check not yet implemented";

            return health;
        }

        private ComponentHealth CheckDisk()
        {
            var health = new ComponentHealth();

            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(Environment.CurrentDirectory) ?? "C:\\");
                var freeSpaceGB = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                var totalSpaceGB = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                var freeSpacePercent = (drive.AvailableFreeSpace * 100.0) / drive.TotalSize;

                health.Message = $"Free: {freeSpaceGB:F2} GB / {totalSpaceGB:F2} GB ({freeSpacePercent:F1}%)";

                // Determine status based on free space
                if (freeSpacePercent < 5)
                {
                    health.Status = "unhealthy";
                    health.Message += " - Critical: Less than 5% free space";
                }
                else if (freeSpacePercent < 10)
                {
                    health.Status = "degraded";
                    health.Message += " - Warning: Less than 10% free space";
                }
                else
                {
                    health.Status = "healthy";
                }
            }
            catch (Exception ex)
            {
                health.Status = "degraded";
                health.Message = $"Disk check failed: {ex.Message}";
                _logger.LogWarning(ex, "Disk health check failed");
            }

            return health;
        }

        private static string DetermineOverallStatus(HealthCheckDetails checks)
        {
            // If any component is unhealthy, overall status is unhealthy
            if (checks.RabbitMQ.Status == "unhealthy" ||
                checks.Disk.Status == "unhealthy")
            {
                return "unhealthy";
            }

            // If any component is degraded, overall status is degraded
            if (checks.RabbitMQ.Status == "degraded" ||
                checks.Disk.Status == "degraded")
            {
                return "degraded";
            }

            return "healthy";
        }
    }
}
