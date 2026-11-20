using MngExtensions.Application.Pdks;
using StackExchange.Redis;

namespace MngExtensions.Persistence.Services;

public sealed class HealthCheckService : IHealthCheckService
{
    private readonly IConnectionMultiplexer _redis;

    public HealthCheckService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken)
    {
        try
        {
            var pong = await _redis.GetDatabase().PingAsync().WaitAsync(cancellationToken);
            return new HealthCheckResult
            {
                Status = "healthy",
                Timestamp = DateTime.UtcNow,
                Redis = $"ok {pong.TotalMilliseconds:0}ms"
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                Status = "unhealthy",
                Timestamp = DateTime.UtcNow,
                Redis = ex.Message
            };
        }
    }
}
