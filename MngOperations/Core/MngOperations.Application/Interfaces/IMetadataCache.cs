using MngOperations.Application.Models;

namespace MngOperations.Application.Interfaces;

public interface IMetadataCache
{
    Task<WorkspaceRecord> GetWorkspaceAsync(string workspaceId, string token, CancellationToken cancellationToken = default);
    Task<WorkItemTypeRecord> GetWorkItemTypeAsync(string typeId, string token, CancellationToken cancellationToken = default);
    Task<StateFlowRecord> GetStateFlowAsync(string stateFlowId, string token, CancellationToken cancellationToken = default);

    Task<BoardRecord> GetBoardAsync(string boardId, string token, CancellationToken cancellationToken = default);

    Task<FormRecord?> ResolveDefaultFormAsync(
        string workspaceId,
        string token,
        CancellationToken cancellationToken = default);

    Task<FormRecord> GetFormAsync(string formId, string token, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RuleRecord>> GetRulesForWorkspaceAsync(
        string workspaceId,
        string token,
        CancellationToken cancellationToken = default);

    Task<FieldRecord> GetFieldAsync(string fieldId, string token, CancellationToken cancellationToken = default);

    Task<FieldRecord?> FindFieldByKeyAsync(string fieldKey, string token, CancellationToken cancellationToken = default);

    Task<ProfileRecord?> ResolveDefaultProfileAsync(
        string workspaceId,
        string token,
        CancellationToken cancellationToken = default);

    Task<StateRecord> GetStateAsync(string stateId, string token, CancellationToken cancellationToken = default);

    Task<SlaPolicyRecord?> ResolveSlaPolicyAsync(
        string workspaceId,
        string typeId,
        string? priorityId,
        string token,
        CancellationToken cancellationToken = default);

    Task<DashboardRecord> GetDashboardAsync(string dashboardId, string token, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationPolicyRecord>> GetNotificationPoliciesForWorkspaceAsync(
        string workspaceId,
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>Global katalog listesi (states/priorities/types/fields) — lazy yüklenir, ayrı TTL ile cache'lenir.</summary>
    Task<IReadOnlyList<Dictionary<string, object?>>> GetCatalogListAsync(
        string dataset,
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>Write-through: katalog yazısından sonra ilgili liste cache'ini düşür.</summary>
    void InvalidateCatalog(string dataset);
}
