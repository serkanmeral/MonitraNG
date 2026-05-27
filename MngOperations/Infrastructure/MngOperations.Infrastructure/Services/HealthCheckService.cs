using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngOperations.Application.Configuration;
using MngOperations.Application.Interfaces;

namespace MngOperations.Infrastructure.Services;

public class HealthCheckService : IHealthCheckService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MngOperationsSettings _settings;
    private readonly ILogger<HealthCheckService> _logger;

    public HealthCheckService(
        IHttpClientFactory httpClientFactory,
        IOptions<MngOperationsSettings> settings,
        ILogger<HealthCheckService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<HealthReport> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var checks = new Dictionary<string, ComponentHealth>();

        var dg = await CheckDataGatewayAsync(cancellationToken);
        checks["dataGateway"] = dg;

        checks["rabbitMq"] = new ComponentHealth
        {
            Status = "healthy",
            Message = "Lazy connect on first publish"
        };

        checks["mngNotifiers"] = await CheckMngNotifiersAsync(cancellationToken);

        var dgStatus = dg.Status;
        var notifierStatus = checks["mngNotifiers"].Status;
        var overall = dgStatus == "degraded" || notifierStatus == "degraded" ? "degraded" : "healthy";

        return new HealthReport
        {
            Status = overall,
            Timestamp = DateTime.UtcNow,
            Checks = checks
        };
    }

    private async Task<ComponentHealth> CheckDataGatewayAsync(CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("HealthCheck");
            var baseUrl = _settings.DataGateway.BaseUrl.TrimEnd('/');
            var version = _settings.DataGateway.ApiVersion;
            var url = $"{baseUrl}/api/{version}/health";

            using var response = await client.GetAsync(url, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return new ComponentHealth { Status = "healthy", Message = "Reachable" };
            }

            return new ComponentHealth
            {
                Status = "degraded",
                Message = $"HTTP {(int)response.StatusCode}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DataGateway health probe failed");
            return new ComponentHealth { Status = "degraded", Message = ex.Message };
        }
    }

    private async Task<ComponentHealth> CheckMngNotifiersAsync(CancellationToken cancellationToken)
    {
        if (!_settings.MngNotifiers.Enabled)
        {
            return new ComponentHealth { Status = "healthy", Message = "Disabled" };
        }

        try
        {
            var client = _httpClientFactory.CreateClient("HealthCheck");
            var baseUrl = _settings.MngNotifiers.BaseUrl.TrimEnd('/');
            var version = string.IsNullOrWhiteSpace(_settings.MngNotifiers.ApiVersion)
                ? "v1"
                : _settings.MngNotifiers.ApiVersion.Trim();
            var url = $"{baseUrl}/api/{version}/health";

            using var response = await client.GetAsync(url, cancellationToken);
            return response.IsSuccessStatusCode
                ? new ComponentHealth { Status = "healthy", Message = "Reachable" }
                : new ComponentHealth { Status = "degraded", Message = $"HTTP {(int)response.StatusCode}" };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MngNotifiers health probe failed");
            return new ComponentHealth { Status = "degraded", Message = ex.Message };
        }
    }
}
