using MngOperations.Application.Contracts.WorkspacePacks;

namespace MngOperations.Application.Interfaces;

public interface IWorkspacePackInstallService
{
    Task<WorkspacePackInstallDto?> GetAsync(string workspaceId, CancellationToken cancellationToken = default);

    Task<WorkspacePackInstallDto> InstallAsync(
        string workspaceId,
        InstallWorkspacePackRequest request,
        CancellationToken cancellationToken = default);
}
