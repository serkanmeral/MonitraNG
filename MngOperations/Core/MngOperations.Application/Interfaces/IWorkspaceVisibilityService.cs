using MngOperations.Application.Contracts.Workspaces;

namespace MngOperations.Application.Interfaces;

public interface IWorkspaceVisibilityService
{
    Task<IReadOnlyList<VisibleWorkspaceItem>> ListVisibleAsync(CancellationToken cancellationToken = default);
}
