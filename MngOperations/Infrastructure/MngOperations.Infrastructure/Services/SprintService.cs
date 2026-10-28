using System.Net;
using MngOperations.Application.Contracts.Sprints;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Interfaces;
using MngOperations.Application.Permissions;
using MngOperations.Application.Utilities;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed class SprintService : ISprintService
{
    private const int PageSize = 200;

    private readonly IMngDataGatewayClient _dg;
    private readonly IMetadataCache _metadataCache;
    private readonly IPermissionEvaluator _permissions;
    private readonly IRequestContext _requestContext;

    public SprintService(
        IMngDataGatewayClient dg,
        IMetadataCache metadataCache,
        IPermissionEvaluator permissions,
        IRequestContext requestContext)
    {
        _dg = dg;
        _metadataCache = metadataCache;
        _permissions = permissions;
        _requestContext = requestContext;
    }

    public async Task<IReadOnlyList<SprintDto>> ListAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        var token = RequireToken();
        await EnsureWorkspaceAsync(workspaceId, WorkspaceAction.View, token, cancellationToken);
        var rows = await LoadSprintsAsync(workspaceId, token, cancellationToken);
        var counts = await CountItemsAsync(workspaceId, rows.Select(r => WorkItemDataHelper.GetDataId(r)), token, cancellationToken);
        return rows
            .Select(row => Map(row, counts.GetValueOrDefault(WorkItemDataHelper.GetDataId(row))))
            .OrderBy(OrderRank)
            .ThenBy(s => s.StartDate, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<SprintDto> CreateAsync(string workspaceId, CreateSprintRequest request, CancellationToken cancellationToken = default)
    {
        var token = RequireToken();
        var id = await EnsureWorkspaceAsync(workspaceId, WorkspaceAction.Edit, token, cancellationToken);
        var (name, goal, start, end) = ReadForm(request.Name, request.Goal, request.StartDate, request.EndDate);
        var existing = await LoadSprintsAsync(id, token, cancellationToken);
        EnsureUniqueName(existing, name, exceptId: null);

        var created = await WriteAsync(
            () => _dg.CreateAsync(OcDatasets.Sprints, new Dictionary<string, object?>
            {
                ["workspaceId"] = id,
                ["name"] = name,
                ["goal"] = goal,
                ["startDate"] = start,
                ["endDate"] = end,
                ["state"] = SprintRules.Planned
            }, token, cancellationToken),
            cancellationToken);

        return Map(created, 0);
    }

    public async Task<SprintDto> UpdateAsync(
        string workspaceId,
        string sprintId,
        UpdateSprintRequest request,
        CancellationToken cancellationToken = default)
    {
        var token = RequireToken();
        var id = await EnsureWorkspaceAsync(workspaceId, WorkspaceAction.Edit, token, cancellationToken);
        var current = await RequireSprintAsync(id, sprintId, token, cancellationToken);
        if (SprintRules.NormalizeState(WorkItemDataHelper.GetString(current, "state")) == SprintRules.Closed)
            throw Closed();

        var (name, goal, start, end) = ReadForm(request.Name, request.Goal, request.StartDate, request.EndDate);
        var existing = await LoadSprintsAsync(id, token, cancellationToken);
        EnsureUniqueName(existing, name, sprintId.Trim());

        var updated = await WriteAsync(
            () => _dg.UpdateAsync(OcDatasets.Sprints, sprintId.Trim(), new Dictionary<string, object?>
            {
                ["name"] = name,
                ["goal"] = goal,
                ["startDate"] = start,
                ["endDate"] = end
            }, token, cancellationToken),
            cancellationToken);

        var count = await CountOneAsync(id, sprintId.Trim(), token, cancellationToken);
        return Map(updated, count);
    }

    public async Task<SprintDto> ActivateAsync(string workspaceId, string sprintId, CancellationToken cancellationToken = default)
    {
        var token = RequireToken();
        var id = await EnsureWorkspaceAsync(workspaceId, WorkspaceAction.Edit, token, cancellationToken);
        var current = await RequireSprintAsync(id, sprintId, token, cancellationToken);
        var state = SprintRules.NormalizeState(WorkItemDataHelper.GetString(current, "state"));
        if (state == SprintRules.Active)
            return Map(current, await CountOneAsync(id, sprintId.Trim(), token, cancellationToken));
        if (state != SprintRules.Planned)
            throw Closed();

        var rows = await LoadSprintsAsync(id, token, cancellationToken);
        if (rows.Any(row =>
                !string.Equals(WorkItemDataHelper.GetDataId(row), sprintId.Trim(), StringComparison.Ordinal)
                && SprintRules.NormalizeState(WorkItemDataHelper.GetString(row, "state")) == SprintRules.Active))
        {
            throw new OperationCoreException(
                "SPRINT_ALREADY_ACTIVE",
                "This workspace already has an active sprint.",
                "Bu workspace'te zaten aktif bir sprint var.",
                409);
        }

        var updated = await WriteAsync(
            () => _dg.UpdateAsync(OcDatasets.Sprints, sprintId.Trim(), new Dictionary<string, object?>
            {
                ["state"] = SprintRules.Active
            }, token, cancellationToken),
            cancellationToken);
        return Map(updated, await CountOneAsync(id, sprintId.Trim(), token, cancellationToken));
    }

    public async Task<SprintDto> CloseAsync(string workspaceId, string sprintId, CancellationToken cancellationToken = default)
    {
        var token = RequireToken();
        var id = await EnsureWorkspaceAsync(workspaceId, WorkspaceAction.Edit, token, cancellationToken);
        var current = await RequireSprintAsync(id, sprintId, token, cancellationToken);
        var state = SprintRules.NormalizeState(WorkItemDataHelper.GetString(current, "state"));
        if (state == SprintRules.Closed)
            return Map(current, await CountOneAsync(id, sprintId.Trim(), token, cancellationToken));
        if (state != SprintRules.Active)
        {
            throw new OperationCoreException(
                "SPRINT_NOT_ACTIVE",
                "Only the active sprint can be closed.",
                "Yalnızca aktif sprint kapatılır.",
                400);
        }

        var updated = await WriteAsync(
            () => _dg.UpdateAsync(OcDatasets.Sprints, sprintId.Trim(), new Dictionary<string, object?>
            {
                ["state"] = SprintRules.Closed
            }, token, cancellationToken),
            cancellationToken);
        return Map(updated, await CountOneAsync(id, sprintId.Trim(), token, cancellationToken));
    }

    public async Task DeleteAsync(string workspaceId, string sprintId, CancellationToken cancellationToken = default)
    {
        var token = RequireToken();
        var id = await EnsureWorkspaceAsync(workspaceId, WorkspaceAction.Edit, token, cancellationToken);
        var current = await RequireSprintAsync(id, sprintId, token, cancellationToken);
        if (SprintRules.NormalizeState(WorkItemDataHelper.GetString(current, "state")) != SprintRules.Planned)
        {
            throw new OperationCoreException(
                "SPRINT_NOT_PLANNED",
                "Only a planned sprint with no work items can be deleted.",
                "Yalnızca işi olmayan planlı sprint silinir.",
                400);
        }

        if (await CountOneAsync(id, sprintId.Trim(), token, cancellationToken) > 0)
        {
            throw new OperationCoreException(
                "SPRINT_NOT_EMPTY",
                "Move the work items out before deleting the sprint.",
                "Sprint silinmeden önce işler çıkarılmalı.",
                409);
        }

        try
        {
            var deleted = await _dg.DeleteAsync(OcDatasets.Sprints, sprintId.Trim(), token, cancellationToken);
            if (!deleted)
                throw NotFound();
        }
        catch (HttpRequestException ex)
        {
            throw StorageError(ex);
        }
    }

    public async Task<IReadOnlyList<SprintItemDto>> ListItemsAsync(
        string workspaceId,
        string sprintId,
        CancellationToken cancellationToken = default)
    {
        var token = RequireToken();
        var id = await EnsureWorkspaceAsync(workspaceId, WorkspaceAction.View, token, cancellationToken);
        await RequireSprintAsync(id, sprintId, token, cancellationToken);
        var page = await QueryItemsAsync(id, sprintId.Trim(), PageSize, token, cancellationToken);
        return page.Items
            .Select(row => new SprintItemDto
            {
                Id = WorkItemDataHelper.GetDataId(row),
                Key = WorkItemDataHelper.GetString(row, "key") ?? string.Empty,
                Title = WorkItemDataHelper.GetString(row, "title") ?? string.Empty,
                StateId = WorkItemDataHelper.GetString(row, "stateId")
            })
            .Where(item => item.Id.Length > 0)
            .ToList();
    }

    public async Task SetMembershipAsync(
        string workspaceId,
        SetSprintMembershipRequest request,
        CancellationToken cancellationToken = default)
    {
        var token = RequireToken();
        var id = RequireId(workspaceId);
        var workItemId = RequireId(request.WorkItemId);
        var sprintId = string.IsNullOrWhiteSpace(request.SprintId) ? null : request.SprintId.Trim();

        var workItem = await _dg.GetByIdAsync<Dictionary<string, object?>>(
            OcDatasets.WorkItems, workItemId, token, cancellationToken, expand: false);
        if (workItem == null)
        {
            throw new OperationCoreException(
                "WORK_ITEM_NOT_FOUND",
                $"Work item '{workItemId}' not found.",
                $"İş kaydı '{workItemId}' bulunamadı.",
                404);
        }

        var itemWorkspace = WorkItemDataHelper.GetString(workItem, "workspaceId");
        if (!string.Equals(itemWorkspace, id, StringComparison.Ordinal))
        {
            throw new OperationCoreException(
                "SPRINT_WORKSPACE_MISMATCH",
                "The work item belongs to another workspace.",
                "İş kaydı başka bir workspace'e ait.",
                400);
        }

        var workspace = await _metadataCache.GetWorkspaceAsync(id, token, cancellationToken);
        _permissions.EnsureWorkItemUpdate(workspace, workItem);

        if (sprintId == null)
        {
            await WriteMembershipAsync(workItemId, string.Empty, token, cancellationToken);
            return;
        }

        var sprint = await RequireSprintAsync(id, sprintId, token, cancellationToken);
        if (SprintRules.NormalizeState(WorkItemDataHelper.GetString(sprint, "state")) == SprintRules.Closed)
            throw Closed();

        var typeId = WorkItemDataHelper.GetString(workItem, "typeId");
        if (string.IsNullOrWhiteSpace(typeId))
        {
            throw new OperationCoreException(
                "TYPE_REQUIRED",
                "The work item has no type.",
                "İş kaydının tipi yok.",
                400);
        }

        var type = await _dg.GetByIdAsync<Dictionary<string, object?>>(
            OcDatasets.WorkItemTypes, typeId, token, cancellationToken, expand: false);
        if (!SprintRules.CanJoin(type))
        {
            throw new OperationCoreException(
                "SPRINT_PARENT_TYPE",
                "A parent work type cannot join a sprint.",
                "Üst iş sprinte girmez.",
                400);
        }

        await WriteMembershipAsync(workItemId, sprintId, token, cancellationToken);
    }

    private async Task WriteMembershipAsync(string workItemId, string sprintId, string token, CancellationToken cancellationToken)
    {
        await WriteAsync(
            () => _dg.UpdateAsync(OcDatasets.WorkItems, workItemId, new Dictionary<string, object?>
            {
                ["sprintId"] = sprintId
            }, token, cancellationToken),
            cancellationToken);
    }

    private async Task<Dictionary<string, int>> CountItemsAsync(
        string workspaceId,
        IEnumerable<string> sprintIds,
        string token,
        CancellationToken cancellationToken)
    {
        var ids = sprintIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        var counts = ids.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
        if (ids.Count == 0)
            return counts;

        var page = await _dg.QueryPageAsync(
            OcDatasets.WorkItems,
            new Dictionary<string, object?>
            {
                ["workspaceId"] = workspaceId,
                ["sprintId"] = new Dictionary<string, object?> { ["$in"] = ids.Cast<object?>().ToList() }
            },
            "limit=2000&skip=0&expand=false",
            token,
            cancellationToken);

        foreach (var row in page.Items)
        {
            var sprintId = WorkItemDataHelper.GetString(row, "sprintId");
            if (sprintId != null && counts.ContainsKey(sprintId))
                counts[sprintId]++;
        }

        return counts;
    }

    private async Task<int> CountOneAsync(string workspaceId, string sprintId, string token, CancellationToken cancellationToken)
    {
        var page = await QueryItemsAsync(workspaceId, sprintId, 1, token, cancellationToken);
        return (int)Math.Min(page.Total, int.MaxValue);
    }

    private Task<DataGatewayPage> QueryItemsAsync(
        string workspaceId,
        string sprintId,
        int limit,
        string token,
        CancellationToken cancellationToken) =>
        _dg.QueryPageAsync(
            OcDatasets.WorkItems,
            new Dictionary<string, object?>
            {
                ["workspaceId"] = workspaceId,
                ["sprintId"] = sprintId
            },
            $"limit={limit}&skip=0&expand=false&sort=key",
            token,
            cancellationToken);

    private async Task<List<Dictionary<string, object?>>> LoadSprintsAsync(
        string workspaceId,
        string token,
        CancellationToken cancellationToken)
    {
        var page = await _dg.QueryPageAsync(
            OcDatasets.Sprints,
            new Dictionary<string, object?> { ["workspaceId"] = workspaceId },
            "limit=200&skip=0&expand=false&sort=startDate",
            token,
            cancellationToken);
        return page.Items.ToList();
    }

    private async Task<Dictionary<string, object?>> RequireSprintAsync(
        string workspaceId,
        string sprintId,
        string token,
        CancellationToken cancellationToken)
    {
        var id = RequireId(sprintId);
        var row = await _dg.GetByIdAsync<Dictionary<string, object?>>(OcDatasets.Sprints, id, token, cancellationToken, expand: false);
        if (row == null || !string.Equals(WorkItemDataHelper.GetString(row, "workspaceId"), workspaceId, StringComparison.Ordinal))
            throw NotFound();
        return row;
    }

    private async Task<string> EnsureWorkspaceAsync(
        string workspaceId,
        WorkspaceAction action,
        string token,
        CancellationToken cancellationToken)
    {
        var id = RequireId(workspaceId);
        var workspace = await _metadataCache.GetWorkspaceAsync(id, token, cancellationToken);
        _permissions.EnsureWorkspace(workspace, action);
        return id;
    }

    private static void EnsureUniqueName(IEnumerable<Dictionary<string, object?>> rows, string name, string? exceptId)
    {
        var taken = rows.Any(row =>
            !string.Equals(WorkItemDataHelper.GetDataId(row), exceptId, StringComparison.Ordinal)
            && string.Equals(WorkItemDataHelper.GetString(row, "name"), name, StringComparison.OrdinalIgnoreCase));
        if (!taken)
            return;

        throw new OperationCoreException(
            "SPRINT_NAME_TAKEN",
            "A sprint with this name already exists in the workspace.",
            "Bu workspace'te aynı adlı sprint var.",
            409);
    }

    private static (string Name, string Goal, string Start, string End) ReadForm(
        string? name,
        string? goal,
        string? startRaw,
        string? endRaw)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 || trimmed.Length > SprintRules.MaxNameLength)
        {
            throw new OperationCoreException(
                "SPRINT_NAME_INVALID",
                "Sprint name is required and must be at most 80 characters.",
                "Sprint adı zorunludur ve en fazla 80 karakter olabilir.",
                400);
        }

        var goalText = goal?.Trim() ?? string.Empty;
        if (goalText.Length > SprintRules.MaxGoalLength)
        {
            throw new OperationCoreException(
                "SPRINT_GOAL_INVALID",
                "Sprint goal must be at most 500 characters.",
                "Sprint hedefi en fazla 500 karakter olabilir.",
                400);
        }

        if (!SprintRules.TryParseDate(startRaw, out var start) || !SprintRules.TryParseDate(endRaw, out var end))
        {
            throw new OperationCoreException(
                "SPRINT_DATE_INVALID",
                "Start and end dates are required.",
                "Başlangıç ve bitiş tarihleri zorunludur.",
                400);
        }

        if (string.CompareOrdinal(end, start) < 0)
        {
            throw new OperationCoreException(
                "SPRINT_DATE_ORDER",
                "The end date cannot be before the start date.",
                "Bitiş tarihi başlangıçtan önce olamaz.",
                400);
        }

        return (trimmed, goalText, start, end);
    }

    private static SprintDto Map(IReadOnlyDictionary<string, object?> row, int itemCount) =>
        new()
        {
            Id = WorkItemDataHelper.GetDataId(row),
            WorkspaceId = WorkItemDataHelper.GetString(row, "workspaceId") ?? string.Empty,
            Name = WorkItemDataHelper.GetString(row, "name") ?? string.Empty,
            Goal = WorkItemDataHelper.GetString(row, "goal"),
            StartDate = WorkItemDataHelper.GetString(row, "startDate") ?? string.Empty,
            EndDate = WorkItemDataHelper.GetString(row, "endDate") ?? string.Empty,
            State = SprintRules.NormalizeState(WorkItemDataHelper.GetString(row, "state")),
            ItemCount = itemCount
        };

    private static int OrderRank(SprintDto sprint) => sprint.State switch
    {
        SprintRules.Active => 0,
        SprintRules.Planned => 1,
        _ => 2
    };

    private static async Task<Dictionary<string, object?>> WriteAsync(
        Func<Task<Dictionary<string, object?>>> call,
        CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        try
        {
            return await call();
        }
        catch (HttpRequestException ex)
        {
            throw StorageError(ex);
        }
    }

    private string RequireToken()
    {
        if (string.IsNullOrEmpty(_requestContext.BearerToken))
        {
            throw new OperationCoreException(
                "UNAUTHORIZED",
                "A bearer token is required.",
                "Oturum gerekli.",
                401);
        }

        return _requestContext.BearerToken;
    }

    private static string RequireId(string? id)
    {
        var value = id?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            throw new OperationCoreException(
                "VALIDATION_ERROR",
                "An identifier is required.",
                "Kimlik zorunludur.",
                400);
        }

        return value;
    }

    private static OperationCoreException NotFound() =>
        new("SPRINT_NOT_FOUND", "Sprint was not found.", "Sprint bulunamadı.", 404);

    private static OperationCoreException Closed() =>
        new(
            "SPRINT_CLOSED",
            "A closed sprint cannot take work items or be edited.",
            "Kapanmış sprinte iş girmez ve düzenlenmez.",
            400);

    private static OperationCoreException StorageError(HttpRequestException ex) =>
        new(
            "SPRINT_STORAGE",
            ex.Message,
            "Sprint kaydı yazılamadı.",
            ex.StatusCode == HttpStatusCode.NotFound ? 404 : 502);
}
