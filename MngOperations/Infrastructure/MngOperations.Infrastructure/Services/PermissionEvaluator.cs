using System.Text.Json;
using Microsoft.Extensions.Logging;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Interfaces;
using MngOperations.Application.Models;
using MngOperations.Application.Permissions;
using MngOperations.Application.Utilities;

namespace MngOperations.Infrastructure.Services;

public class PermissionEvaluator : IPermissionEvaluator
{
    private readonly IRequestContext _context;
    private readonly ILogger<PermissionEvaluator> _logger;

    public PermissionEvaluator(IRequestContext context, ILogger<PermissionEvaluator> logger)
    {
        _context = context;
        _logger = logger;
    }

    public void EnsureWorkspace(WorkspaceRecord workspace, WorkspaceAction action)
    {
        if (HasPlatformBypass())
        {
            LogPlatformBypass($"workspace.{action}", workspace.DataId);
            return;
        }

        if (_context.IsManager && action is WorkspaceAction.View or WorkspaceAction.Create or WorkspaceAction.Edit or WorkspaceAction.Admin)
            return;

        var allowed = action switch
        {
            WorkspaceAction.View => CanViewWorkspace(workspace),
            WorkspaceAction.Create => CanCreateInWorkspace(workspace),
            WorkspaceAction.Edit => CanEditInWorkspace(workspace),
            WorkspaceAction.Admin => CanAdminWorkspace(workspace),
            _ => false
        };

        if (!allowed)
        {
            throw new OperationCoreException(
                "WORKSPACE_FORBIDDEN",
                $"You do not have permission to {action.ToString().ToLowerInvariant()} in this workspace.",
                $"Bu workspace'te {action} yetkiniz yok.",
                403);
        }
    }

    public void EnsureWorkItemView(WorkspaceRecord workspace, IReadOnlyDictionary<string, object?> workItem)
    {
        if (CanViewWorkItem(workspace, workItem))
            return;

        throw new OperationCoreException(
            "WORK_ITEM_FORBIDDEN",
            "You cannot view this work item.",
            "Bu iş kaydını görüntüleme yetkiniz yok.",
            403);
    }

    public void EnsureWorkItemUpdate(WorkspaceRecord workspace, IReadOnlyDictionary<string, object?> workItem)
    {
        if (CanEditWorkItem(workspace, workItem))
            return;

        throw new OperationCoreException(
            "WORK_ITEM_FORBIDDEN",
            "You cannot update this work item.",
            "Bu iş kaydını güncelleme yetkiniz yok.",
            403);
    }

    public bool CanViewWorkItem(WorkspaceRecord workspace, IReadOnlyDictionary<string, object?> workItem)
    {
        if (HasPlatformBypass() || _context.IsManager)
            return true;

        return CanViewWorkspace(workspace) || IsAssignee(workItem);
    }

    public bool CanEditWorkItem(WorkspaceRecord workspace, IReadOnlyDictionary<string, object?> workItem)
    {
        if (HasPlatformBypass() || _context.IsManager)
            return true;

        return CanEditInWorkspace(workspace) || IsAssignee(workItem);
    }

    public void EnsureTransition(
        WorkspaceRecord workspace,
        JsonElement transition,
        IReadOnlyDictionary<string, object?> workItem)
    {
        var transitionKey = StateFlowCatalog.GetStringProperty(transition, "transitionKey") ?? "?";

        if (HasPlatformBypass())
        {
            LogPlatformBypass($"transition.{transitionKey}", GetString(workItem, "key"));
            return;
        }

        if (_context.IsManager)
            return;

        EnsureWorkItemUpdate(workspace, workItem);

        var groups = StateFlowCatalog.GetPermissionGroups(StateFlowCatalog.GetPermissions(transition));
        if (!GroupListParser.Intersects(_context.UserGroups, groups))
        {
            throw new OperationCoreException(
                "TRANSITION_FORBIDDEN",
                $"You cannot apply transition '{transitionKey}'.",
                $"'{transitionKey}' transition'ını uygulama yetkiniz yok.",
                403);
        }
    }

    public bool CanApplyTransition(WorkspaceRecord workspace, JsonElement transition)
    {
        if (HasPlatformBypass() || _context.IsManager)
            return true;

        if (!CanEditInWorkspace(workspace) && !CanViewWorkspace(workspace))
            return false;

        var groups = StateFlowCatalog.GetPermissionGroups(StateFlowCatalog.GetPermissions(transition));
        return GroupListParser.Intersects(_context.UserGroups, groups);
    }

    public IReadOnlyList<JsonElement> GetAvailableTransitions(
        WorkspaceRecord workspace,
        StateFlowRecord stateFlow,
        string currentStateId)
    {
        return StateFlowCatalog.ListFromState(stateFlow.Transitions, currentStateId)
            .Where(t => CanApplyTransition(workspace, t))
            .ToList();
    }

    public void EnsureBoardView(WorkspaceRecord workspace, BoardRecord board)
    {
        if (CanViewBoard(workspace, board))
            return;

        throw new OperationCoreException(
            "BOARD_FORBIDDEN",
            "You cannot view this board.",
            "Bu board'u görüntüleme yetkiniz yok.",
            403);
    }

    public bool CanViewBoard(WorkspaceRecord workspace, BoardRecord board)
    {
        if (HasPlatformBypass() || _context.IsManager)
            return true;

        if (!CanViewWorkspace(workspace))
            return false;

        var boardViewGroups = GroupListParser.Parse(board.ViewGroups);
        return GroupListParser.Intersects(_context.UserGroups, boardViewGroups);
    }

    public void EnsureDashboardView(WorkspaceRecord workspace, DashboardRecord dashboard)
    {
        if (CanViewDashboard(workspace, dashboard))
            return;

        throw new OperationCoreException(
            "DASHBOARD_FORBIDDEN",
            "You cannot view this dashboard.",
            "Bu dashboard'u görüntüleme yetkiniz yok.",
            403);
    }

    public bool CanViewDashboard(WorkspaceRecord workspace, DashboardRecord dashboard)
    {
        if (HasPlatformBypass() || _context.IsManager)
            return true;

        if (!CanViewWorkspace(workspace))
            return false;

        if (dashboard.Permissions is not { ValueKind: JsonValueKind.Object } permissions)
            return true;

        if (permissions.TryGetProperty("viewGroups", out var viewGroups))
        {
            var groups = GroupListParser.Parse(viewGroups);
            if (groups.Count > 0)
                return GroupListParser.Intersects(_context.UserGroups, groups);
        }

        return true;
    }

    private bool CanViewWorkspace(WorkspaceRecord workspace)
    {
        var view = GroupListParser.Parse(workspace.ViewGroups);
        var edit = GroupListParser.Parse(workspace.EditGroups);
        var admin = GroupListParser.Parse(workspace.AdminGroups);
        var owner = GroupListParser.Parse(workspace.OwnerGroups);

        return GroupListParser.Intersects(_context.UserGroups, view)
               || GroupListParser.Intersects(_context.UserGroups, edit)
               || GroupListParser.Intersects(_context.UserGroups, admin)
               || GroupListParser.Intersects(_context.UserGroups, owner);
    }

    private bool CanCreateInWorkspace(WorkspaceRecord workspace) =>
        CanEditInWorkspace(workspace)
        || GroupListParser.Intersects(_context.UserGroups, GroupListParser.Parse(workspace.OwnerGroups));

    private bool CanEditInWorkspace(WorkspaceRecord workspace)
    {
        var edit = GroupListParser.Parse(workspace.EditGroups);
        var admin = GroupListParser.Parse(workspace.AdminGroups);
        return GroupListParser.Intersects(_context.UserGroups, edit)
               || GroupListParser.Intersects(_context.UserGroups, admin);
    }

    private bool CanAdminWorkspace(WorkspaceRecord workspace) =>
        GroupListParser.Intersects(_context.UserGroups, GroupListParser.Parse(workspace.AdminGroups));

    private bool IsAssignee(IReadOnlyDictionary<string, object?> workItem)
    {
        var assignee = GetString(workItem, "assignee");
        return !string.IsNullOrEmpty(assignee)
               && !string.IsNullOrEmpty(_context.Username)
               && string.Equals(assignee, _context.Username, StringComparison.OrdinalIgnoreCase);
    }

    private bool HasPlatformBypass() => _context.IsAdmin;

    private void LogPlatformBypass(string action, string? target) =>
        _logger.LogWarning(
            "Platform admin override: user={User} action={Action} target={Target}",
            _context.Username,
            action,
            target);

    private static string? GetString(IReadOnlyDictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var value) || value == null)
            return null;

        return value switch
        {
            string s => s,
            JsonElement el when el.ValueKind == JsonValueKind.String => el.GetString(),
            _ => value.ToString()
        };
    }
}
