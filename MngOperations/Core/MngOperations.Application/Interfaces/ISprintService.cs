using MngOperations.Application.Contracts.Sprints;

namespace MngOperations.Application.Interfaces;

public interface ISprintService
{
    Task<IReadOnlyList<SprintDto>> ListAsync(string workspaceId, CancellationToken cancellationToken = default);

    Task<SprintDto> CreateAsync(string workspaceId, CreateSprintRequest request, CancellationToken cancellationToken = default);

    Task<SprintDto> UpdateAsync(string workspaceId, string sprintId, UpdateSprintRequest request, CancellationToken cancellationToken = default);

    Task<SprintDto> ActivateAsync(string workspaceId, string sprintId, CancellationToken cancellationToken = default);

    Task<SprintDto> CloseAsync(string workspaceId, string sprintId, CancellationToken cancellationToken = default);

    Task DeleteAsync(string workspaceId, string sprintId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SprintItemDto>> ListItemsAsync(string workspaceId, string sprintId, CancellationToken cancellationToken = default);

    Task SetMembershipAsync(string workspaceId, SetSprintMembershipRequest request, CancellationToken cancellationToken = default);
}
