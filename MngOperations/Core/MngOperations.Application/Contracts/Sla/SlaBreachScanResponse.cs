namespace MngOperations.Application.Contracts.Sla;

public sealed class SlaBreachScanResponse
{
    public required string WorkspaceId { get; init; }
    public DateTime ScannedAtUtc { get; init; } = DateTime.UtcNow;
    public int ResponseBreachesProcessed { get; init; }
    public int ResolveBreachesProcessed { get; init; }
    public IReadOnlyList<string> WorkItemIds { get; init; } = Array.Empty<string>();
}
