using MngOperations.Application.Models;

namespace MngOperations.Application.Interfaces;

public interface IMetadataCacheAdminService
{
    Task<MetadataCacheReloadResult> ReloadWorkspaceAsync(
        string workspaceId,
        CancellationToken cancellationToken = default);
}
