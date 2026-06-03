namespace MngScheduler.Application.Interfaces;

public interface ISlaBreachScanOrchestrationService
{
    Task<SlaBreachScanOrchestrationResult> ScanWorkspaceAsync(
        string workspaceId,
        CancellationToken cancellationToken = default);
}

public sealed class SlaBreachScanOrchestrationResult
{
    public bool IsSuccess { get; init; }
    public int HttpStatusCode { get; init; }
    public string? ResponseBody { get; init; }
    public string? ErrorMessage { get; init; }
    public int ResponseBreachesProcessed { get; init; }
    public int ResolveBreachesProcessed { get; init; }
}
