using MngOperations.Application.Models;

namespace MngOperations.Application.Interfaces;

public interface IWorkItemKeyGenerator
{
    Task<string> GenerateNextKeyAsync(
        WorkspaceRecord workspace,
        string workspaceId,
        string token,
        CancellationToken cancellationToken = default);
}
