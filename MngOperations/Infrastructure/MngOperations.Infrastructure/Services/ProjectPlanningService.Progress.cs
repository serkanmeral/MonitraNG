using Microsoft.Extensions.Logging;
using MngOperations.Application.Contracts.Planning;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Models;
using MngOperations.Application.Utilities;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed partial class ProjectPlanningService
{
    public async Task<WbsItemDto> BindWorkItemAsync(string wbsId, BindWbsWorkItemRequest request, CancellationToken ct = default)
    {
        var token = RequireToken();
        var wbs = await LoadWbsOrThrowAsync(wbsId, token, ct);
        var workItemId = (request.WorkItemId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(workItemId))
            throw new OperationCoreException("WI_REQUIRED", "Work item id is required.", "İş kaydı zorunludur.", 400);

        var project = await LoadProjectOrThrowAsync(wbs.projectId!, token, ct);
        if (string.IsNullOrWhiteSpace(project.workspaceId))
            throw new OperationCoreException("WS_REQUIRED", "Project has no workspace.", "Önce projeye bir OC workspace bağlayın.", 400);

        var siblings = await LoadWbsAsync(wbs.projectId!, token, ct);
        if (siblings.Any(x => string.Equals(x.parentId, wbsId, StringComparison.Ordinal)))
            throw new OperationCoreException("WI_LEAF", "Only leaf WBS items can bind a work item.", "Yalnızca yaprak WBS kalemine iş bağlanır.", 400);

        var wi = await _dg.GetByIdAsync<Dictionary<string, object?>>(OcDatasets.WorkItems, workItemId, token, ct, expand: false);
        if (wi is null)
            throw new OperationCoreException("NOT_FOUND", "Work item not found.", "İş kaydı bulunamadı.", 404);

        var wiWorkspace = WorkItemDataHelper.GetPersonRefId(wi, "workspaceId")
            ?? WorkItemDataHelper.GetString(wi, "workspaceId");
        if (!string.Equals(wiWorkspace, project.workspaceId, StringComparison.Ordinal))
            throw new OperationCoreException("WI_WORKSPACE", "Work item is not in the project workspace.", "İş kaydı proje workspace'inde değil.", 400);

        var taken = await FindWbsByWorkItemAsync(workItemId, token, ct);
        if (taken is not null && !string.Equals(taken.__dataId, wbsId, StringComparison.Ordinal))
            throw new OperationCoreException("WI_BOUND", "Work item is already bound to another WBS item.", "Bu iş kaydı başka bir WBS kalemine bağlı.", 409);

        var progress = await ResolveWorkItemProgressAsync(wi, token, ct);
        await _dg.UpdateAsync(PmDatasets.WbsItems, wbsId, new Dictionary<string, object?>
        {
            ["workItemId"] = workItemId,
            ["percentComplete"] = progress.Percent,
            ["actualFinish"] = progress.Closed ? (object?)DateTime.UtcNow : null
        }, token, ct);

        await RecalcProjectProgressAsync(wbs.projectId!, token, ct);
        var row = await LoadWbsOrThrowAsync(wbsId, token, ct);
        var dto = ToWbsDto(row);
        await HydrateWorkItemsAsync(new List<WbsItemDto> { dto }, token, ct);
        return dto;
    }

    public async Task<WbsItemDto> UnbindWorkItemAsync(string wbsId, CancellationToken ct = default)
    {
        var token = RequireToken();
        var wbs = await LoadWbsOrThrowAsync(wbsId, token, ct);
        await _dg.UpdateAsync(PmDatasets.WbsItems, wbsId, new Dictionary<string, object?>
        {
            ["workItemId"] = null
        }, token, ct);
        await RecalcProjectProgressAsync(wbs.projectId!, token, ct);
        var row = await LoadWbsOrThrowAsync(wbsId, token, ct);
        return ToWbsDto(row);
    }

    public async Task<IReadOnlyList<WorkItemCandidateDto>> SearchWorkItemsAsync(
        string projectId,
        string? query,
        CancellationToken ct = default)
    {
        var token = RequireToken();
        var project = await LoadProjectOrThrowAsync(projectId, token, ct);
        if (string.IsNullOrWhiteSpace(project.workspaceId))
            return Array.Empty<WorkItemCandidateDto>();

        var page = await _dg.QueryPageAsync(
            OcDatasets.WorkItems,
            new Dictionary<string, object?> { ["workspaceId"] = project.workspaceId },
            "limit=80&sort=-createdAt&expand=false",
            token,
            ct);

        var q = query?.Trim();
        var mapped = page.Items
            .Select(row => (Dto: MapWorkItemCandidate(row), Row: row))
            .ToList();
        if (!string.IsNullOrEmpty(q))
        {
            mapped = mapped
                .Where(i =>
                    i.Dto.Key.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || i.Dto.Title.Contains(q, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var result = new List<WorkItemCandidateDto>();
        foreach (var item in mapped.Take(20))
        {
            await FillCandidateStateAsync(item.Dto, item.Row, token, ct);
            result.Add(item.Dto);
        }
        return result;
    }

    public async Task<ProjectDetailDto> RecalcProgressAsync(string projectId, CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(projectId, token, ct);
        var wbs = await LoadWbsAsync(projectId, token, ct);
        foreach (var item in wbs.Where(w => !string.IsNullOrWhiteSpace(w.workItemId)))
            await ApplyLinkedPercentAsync(item, token, ct);
        await RecalcProjectProgressAsync(projectId, token, ct);
        return await GetProjectAsync(projectId, ct);
    }

    public async Task ApplyWorkItemProgressAsync(string workItemId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(workItemId)) return;
        var token = RequireToken();
        var wbs = await FindWbsByWorkItemAsync(workItemId, token, ct);
        if (wbs is null || string.IsNullOrWhiteSpace(wbs.projectId)) return;
        await ApplyLinkedPercentAsync(wbs, token, ct);
        await RecalcProjectProgressAsync(wbs.projectId, token, ct);
    }

    public async Task ClearWorkItemLinksAsync(string workItemId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(workItemId)) return;
        var token = RequireToken();
        var wbs = await FindWbsByWorkItemAsync(workItemId, token, ct);
        if (wbs is null || string.IsNullOrWhiteSpace(wbs.__dataId)) return;
        await _dg.UpdateAsync(PmDatasets.WbsItems, wbs.__dataId, new Dictionary<string, object?>
        {
            ["workItemId"] = null
        }, token, ct);
        if (!string.IsNullOrWhiteSpace(wbs.projectId))
            await RecalcProjectProgressAsync(wbs.projectId, token, ct);
    }

    private async Task HydrateWorkItemsAsync(IList<WbsItemDto> items, string token, CancellationToken ct)
    {
        var ids = items
            .Select(i => i.WorkItemId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (ids.Count == 0) return;

        var snapshots = new Dictionary<string, WbsItemDto>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            var wi = await _dg.GetByIdAsync<Dictionary<string, object?>>(OcDatasets.WorkItems, id, token, ct, expand: false);
            if (wi is null) continue;
            var snap = new WbsItemDto
            {
                WorkItemId = id,
                WorkItemKey = WorkItemDataHelper.GetString(wi, "key"),
                WorkItemTitle = WorkItemDataHelper.GetString(wi, "title"),
                WorkItemClosed = WorkItemDataHelper.GetDateTime(wi, "closedAt") is not null
            };
            var stateId = WorkItemDataHelper.GetPersonRefId(wi, "stateId")
                ?? WorkItemDataHelper.GetString(wi, "stateId");
            if (!string.IsNullOrWhiteSpace(stateId))
            {
                try
                {
                    var state = await _metadata.GetStateAsync(stateId, token, ct);
                    snap.WorkItemStateName = state.Name;
                    snap.WorkItemStateCategory = state.Category;
                    snap.WorkItemClosed = state.IsClosed == true || snap.WorkItemClosed;
                }
                catch (OperationCoreException)
                {
                    // State catalog miss must not hide the WBS row.
                }
            }
            snapshots[id] = snap;
        }

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.WorkItemId) || !snapshots.TryGetValue(item.WorkItemId, out var snap))
                continue;
            item.WorkItemKey = snap.WorkItemKey;
            item.WorkItemTitle = snap.WorkItemTitle;
            item.WorkItemStateName = snap.WorkItemStateName;
            item.WorkItemStateCategory = snap.WorkItemStateCategory;
            item.WorkItemClosed = snap.WorkItemClosed;
        }
    }

    private async Task ApplyLinkedPercentAsync(PmWbsRow wbs, string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(wbs.workItemId) || string.IsNullOrWhiteSpace(wbs.__dataId)) return;
        var wi = await _dg.GetByIdAsync<Dictionary<string, object?>>(OcDatasets.WorkItems, wbs.workItemId, token, ct, expand: false);
        if (wi is null) return;
        var progress = await ResolveWorkItemProgressAsync(wi, token, ct);
        await _dg.UpdateAsync(PmDatasets.WbsItems, wbs.__dataId, new Dictionary<string, object?>
        {
            ["percentComplete"] = progress.Percent,
            ["actualFinish"] = progress.Closed ? (wbs.actualFinish ?? DateTime.UtcNow) : null
        }, token, ct);
    }

    private async Task RecalcProjectProgressAsync(string projectId, string token, CancellationToken ct)
    {
        var items = await LoadWbsAsync(projectId, token, ct);
        var children = items
            .Where(w => !string.IsNullOrWhiteSpace(w.parentId) && !string.IsNullOrWhiteSpace(w.__dataId))
            .GroupBy(w => w.parentId!)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        var percents = items
            .Where(w => !string.IsNullOrWhiteSpace(w.__dataId))
            .ToDictionary(w => w.__dataId!, w => w.percentComplete ?? 0, StringComparer.Ordinal);

        double Compute(string id)
        {
            if (!children.TryGetValue(id, out var kids) || kids.Count == 0)
                return percents.TryGetValue(id, out var own) ? own : 0;
            double sum = 0;
            double weight = 0;
            foreach (var kid in kids)
            {
                var kidId = kid.__dataId!;
                var p = Compute(kidId);
                percents[kidId] = p;
                var w = kid.weight is > 0 ? kid.weight.Value : 1;
                sum += p * w;
                weight += w;
            }
            return weight <= 0 ? 0 : Math.Round(sum / weight, 1);
        }

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.__dataId)) continue;
            if (!children.ContainsKey(item.__dataId)) continue;
            var next = ClampPercent(Compute(item.__dataId));
            var current = item.percentComplete ?? 0;
            if (Math.Abs(current - next) < 0.05) continue;
            await _dg.UpdateAsync(PmDatasets.WbsItems, item.__dataId, new Dictionary<string, object?>
            {
                ["percentComplete"] = next
            }, token, ct);
        }
    }

    private async Task<PmWbsRow?> FindWbsByWorkItemAsync(string workItemId, string token, CancellationToken ct)
    {
        var page = await _dg.QueryPageAsync(
            PmDatasets.WbsItems,
            new Dictionary<string, object?> { ["workItemId"] = workItemId },
            "limit=5&expand=false",
            token,
            ct);
        return page.Items.Select(Map<PmWbsRow>).FirstOrDefault(row => !string.IsNullOrWhiteSpace(row.__dataId));
    }

    private async Task<(double Percent, bool Closed)> ResolveWorkItemProgressAsync(
        Dictionary<string, object?> workItem,
        string token,
        CancellationToken ct)
    {
        var closedAt = WorkItemDataHelper.GetDateTime(workItem, "closedAt");
        var stateId = WorkItemDataHelper.GetPersonRefId(workItem, "stateId")
            ?? WorkItemDataHelper.GetString(workItem, "stateId");
        if (!string.IsNullOrWhiteSpace(stateId))
        {
            try
            {
                var state = await _metadata.GetStateAsync(stateId, token, ct);
                if (state.IsClosed == true)
                    return (100, true);
                if (string.Equals(state.Category, "done", StringComparison.OrdinalIgnoreCase))
                    return (100, false);
                if (string.Equals(state.Category, "in_progress", StringComparison.OrdinalIgnoreCase))
                    return (50, false);
            }
            catch (OperationCoreException)
            {
                _logger.LogDebug("State {StateId} not resolved for WBS rollup", stateId);
            }
        }

        if (closedAt is not null)
            return (100, true);
        return (0, false);
    }

    private static WorkItemCandidateDto MapWorkItemCandidate(Dictionary<string, object?> row)
    {
        var id = WorkItemDataHelper.GetDataId(row);
        return new WorkItemCandidateDto
        {
            Id = id,
            Key = WorkItemDataHelper.GetString(row, "key") ?? id,
            Title = WorkItemDataHelper.GetString(row, "title") ?? string.Empty,
            Closed = WorkItemDataHelper.GetDateTime(row, "closedAt") is not null
        };
    }

    private async Task FillCandidateStateAsync(
        WorkItemCandidateDto item,
        Dictionary<string, object?> row,
        string token,
        CancellationToken ct)
    {
        var stateId = WorkItemDataHelper.GetPersonRefId(row, "stateId")
            ?? WorkItemDataHelper.GetString(row, "stateId");
        if (string.IsNullOrWhiteSpace(stateId)) return;
        try
        {
            var state = await _metadata.GetStateAsync(stateId, token, ct);
            item.StateName = state.Name;
            item.StateCategory = state.Category;
            item.Closed = state.IsClosed == true || item.Closed;
        }
        catch (OperationCoreException)
        {
            // Picker still works with key/title.
        }
    }
}
