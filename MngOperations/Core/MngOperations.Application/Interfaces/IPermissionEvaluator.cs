using System.Text.Json;
using MngOperations.Application.Models;
using MngOperations.Application.Permissions;

namespace MngOperations.Application.Interfaces;

public interface IPermissionEvaluator
{
    /// <summary>
    /// Admin ve managers grubu her alanı görür. Diğer kullanıcılar görme, düzenleme,
    /// yönetme veya sahip listesinden biriyle eşleşirse görür. Boş liste herkese açıktır.
    /// </summary>
    bool CanSeeWorkspace(WorkspaceRecord workspace);

    void EnsureWorkspace(WorkspaceRecord workspace, WorkspaceAction action);

    void EnsureWorkItemView(WorkspaceRecord workspace, IReadOnlyDictionary<string, object?> workItem);

    void EnsureWorkItemUpdate(WorkspaceRecord workspace, IReadOnlyDictionary<string, object?> workItem);

    void EnsureTransition(WorkspaceRecord workspace, JsonElement transition, IReadOnlyDictionary<string, object?> workItem);

    bool CanViewWorkItem(WorkspaceRecord workspace, IReadOnlyDictionary<string, object?> workItem);

    bool CanEditWorkItem(WorkspaceRecord workspace, IReadOnlyDictionary<string, object?> workItem);

    bool CanApplyTransition(WorkspaceRecord workspace, JsonElement transition);

    IReadOnlyList<JsonElement> GetAvailableTransitions(
        WorkspaceRecord workspace,
        StateFlowRecord stateFlow,
        string currentStateId);

    void EnsureBoardView(WorkspaceRecord workspace, BoardRecord board);

    bool CanViewBoard(WorkspaceRecord workspace, BoardRecord board);

    void EnsureDashboardView(WorkspaceRecord workspace, DashboardRecord dashboard);

    bool CanViewDashboard(WorkspaceRecord workspace, DashboardRecord dashboard);
}
