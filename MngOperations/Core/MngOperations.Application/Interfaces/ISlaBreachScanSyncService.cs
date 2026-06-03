using MngOperations.Application.Contracts.Sla;

namespace MngOperations.Application.Interfaces;

public interface ISlaBreachScanSyncService
{
    Task<SlaBreachScanSyncResponse> SyncSchedulerJobAsync(
        string workspaceId,
        SlaBreachScanSyncRequest? request = null,
        CancellationToken cancellationToken = default);

    Task UnlinkSchedulerJobAsync(string workspaceId, CancellationToken cancellationToken = default);
}
