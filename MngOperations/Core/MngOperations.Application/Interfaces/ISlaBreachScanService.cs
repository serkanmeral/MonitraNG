using MngOperations.Application.Contracts.Sla;

namespace MngOperations.Application.Interfaces;

public interface ISlaBreachScanService
{
    Task<SlaBreachScanResponse> ScanWorkspaceAsync(string workspaceId, CancellationToken cancellationToken = default);
}
