namespace MngOperations.Application.Interfaces;

public interface IHealthCheckService
{
    Task<HealthReport> GetHealthAsync(CancellationToken cancellationToken = default);
}

public sealed class HealthReport
{
    public required string Status { get; init; }
    public DateTime Timestamp { get; init; }
    public required IReadOnlyDictionary<string, ComponentHealth> Checks { get; init; }
}

public sealed class ComponentHealth
{
    public required string Status { get; init; }
    public string? Message { get; init; }
}
