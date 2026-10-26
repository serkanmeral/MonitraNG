using MngOperations.Application.Contracts.WorkspaceFolders;

namespace MngOperations.Application.Interfaces;

public interface IWorkspaceFolderService
{
    Task<IReadOnlyList<WorkspaceFolderDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<WorkspaceFolderDto> CreateAsync(CreateWorkspaceFolderRequest request, CancellationToken cancellationToken = default);

    Task<WorkspaceFolderDto> UpdateAsync(string id, UpdateWorkspaceFolderRequest request, CancellationToken cancellationToken = default);

    Task<WorkspaceFolderDto> MoveAsync(string id, MoveWorkspaceFolderRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task MoveWorkspaceAsync(string workspaceId, MoveWorkspaceToFolderRequest request, CancellationToken cancellationToken = default);
}
