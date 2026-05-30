using MngOperations.Application.Contracts.Runtime;

namespace MngOperations.Application.Interfaces;

public interface IRuntimeContextService
{
    Task<ProfileRuntimeContext> GetProfileAsync(string workItemId, CancellationToken cancellationToken = default);

    Task<TimelinePage> GetTimelineAsync(
        string workItemId,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);

    Task<BoardRuntimeContext> GetBoardAsync(string boardId, CancellationToken cancellationToken = default);

    Task<QueryExecuteResponse> GetBoardListAsync(
        string boardId,
        BoardListRequest request,
        CancellationToken cancellationToken = default);

    Task<QueryExecuteResponse> ExecuteQueryAsync(
        string queryKey,
        ExecuteQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<FormRuntimeContext> GetFormCreateAsync(
        string workspaceId,
        string? formId = null,
        CancellationToken cancellationToken = default);

    Task<FormRuntimeContext> GetFormEditAsync(
        string workItemId,
        CancellationToken cancellationToken = default);

    Task<StateSegmentsPage> GetStateSegmentsAsync(
        string workItemId,
        CancellationToken cancellationToken = default);

    Task<DashboardRuntimeContext> GetDashboardAsync(
        string dashboardId,
        CancellationToken cancellationToken = default);
}
