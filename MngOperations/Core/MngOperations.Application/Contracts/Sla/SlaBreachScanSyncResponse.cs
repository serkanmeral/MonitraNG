namespace MngOperations.Application.Contracts.Sla;

public sealed class SlaBreachScanSyncRequest
{
    /// <summary>Quartz cron (varsayılan: settings.DefaultCronExpression).</summary>
    public string? CronExpression { get; init; }

    public bool? IsActive { get; init; }
}

public sealed class SlaBreachScanSyncResponse
{
    public required string WorkspaceId { get; init; }
    public required string SchedulerJobId { get; init; }
    public required string CronExpression { get; init; }
    public bool Created { get; init; }
    public bool Updated { get; init; }
}
