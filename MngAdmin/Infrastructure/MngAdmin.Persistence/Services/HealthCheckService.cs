using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Logging;
using MngAdmin.Application.Services;
using MongoDB.Driver;

namespace MngAdmin.Persistence.Services;

/// <summary>
/// Health check service implementation
/// </summary>
public class HealthCheckService : IHealthCheckService
{
    private readonly ILogger<HealthCheckService> _logger;
    private readonly IMongoClient _mongoClient;

    public HealthCheckService(
        ILogger<HealthCheckService> logger,
        IMongoClient mongoClient)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mongoClient = mongoClient ?? throw new ArgumentNullException(nameof(mongoClient));
    }

    public async Task<HealthCheckResult> CheckHealthAsync()
    {
        var result = new HealthCheckResult
        {
            Timestamp = DateTime.UtcNow
        };

        // Check MongoDB
        result.Checks.MongoDB = await CheckMongoDBAsync();

        // Check RabbitMQ (will be implemented when RabbitMQ service is added)
        result.Checks.RabbitMQ = new ComponentHealth
        {
            Status = "healthy",
            Message = "RabbitMQ check not implemented yet"
        };

        // Check Disk
        result.Checks.Disk = CheckDisk();

        // Determine overall status
        result.Status = DetermineOverallStatus(result.Checks);

        return result;
    }

    private async Task<ComponentHealth> CheckMongoDBAsync()
    {
        var health = new ComponentHealth();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Simple ping to check connection
            await _mongoClient.ListDatabaseNamesAsync();
            stopwatch.Stop();

            health.Status = "healthy";
            health.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            health.Message = "MongoDB connection successful";
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            health.Status = "unhealthy";
            health.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            health.Message = $"MongoDB connection failed: {ex.Message}";
            
            _logger.LogError(ex, "MongoDB health check failed");
        }

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
        if (checks.MongoDB.Status == "unhealthy" || 
            checks.RabbitMQ.Status == "unhealthy" ||
            checks.Disk.Status == "unhealthy")
        {
            return "unhealthy";
        }

        // If any component is degraded, overall status is degraded
        if (checks.MongoDB.Status == "degraded" || 
            checks.RabbitMQ.Status == "degraded" ||
            checks.Disk.Status == "degraded")
        {
            return "degraded";
        }

        return "healthy";
    }
}
