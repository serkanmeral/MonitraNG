namespace MngScheduler.Application.Interfaces;

public interface IAlarmValidationOrchestrationService
{
    Task<AlarmValidationOrchestrationResult> RunValidationAsync(
        string domainName,
        CancellationToken cancellationToken = default);
}

public sealed class AlarmValidationOrchestrationResult
{
    public bool IsSuccess { get; init; }
    public int HttpStatusCode { get; init; }
    public string? ResponseBody { get; init; }
    public string? ErrorMessage { get; init; }
    public int CorrelationResolved { get; init; }
    public int ScheduledRaised { get; init; }
    public int ScheduledResolved { get; init; }
}
