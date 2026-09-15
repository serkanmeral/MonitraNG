using Microsoft.Extensions.Logging;
using MngOperations.Application.Contracts.Planning;
using MngOperations.Application.Contracts.WorkItems;
using MngOperations.Application.Models;
using MngOperations.Application.Packs;
using MngOperations.Application.Utilities;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed partial class ProjectPlanningService
{
    private const string PackWorkItemSourceType = "pm_wbs";
    private const string PackWorkItemSourceSystem = "MngOperations";

    private sealed class PackOcWorkItemResult
    {
        public int Created { get; set; }
        public int Skipped { get; set; }
        public int Removed { get; set; }
        public int Kept { get; set; }
    }

    private static bool HasWbsChildren(IReadOnlyList<PmWbsRow> all, string? id) =>
        !string.IsNullOrWhiteSpace(id)
        && all.Any(w => string.Equals(w.parentId, id, StringComparison.Ordinal));

    private static void WalkPackWbs(
        string? parentId,
        IReadOnlyList<JobPackWbsNode> nodes,
        List<PmWbsRow> existing,
        Action<JobPackWbsNode, PmWbsRow?, PmWbsRow?> visit)
    {
        foreach (var node in nodes)
        {
            var name = (node.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name)) continue;
            var match = FindWbsByName(existing, parentId, name);
            var parentMatch = string.IsNullOrWhiteSpace(parentId)
                ? null
                : existing.FirstOrDefault(w => string.Equals(w.__dataId, parentId, StringComparison.Ordinal));
            visit(node, match, parentMatch);
            if (node.Children is { Count: > 0 })
                WalkPackWbs(match?.__dataId, node.Children, existing, visit);
        }
    }

    private PackOcWorkItemResult PreviewPackOcWorkItems(
        string? workspaceId,
        bool workspaceWillExist,
        JobPackDefinition pack,
        List<PmWbsRow> existing,
        string intent,
        IReadOnlyList<PackPreviewItemDto>? detachItems = null)
    {
        var result = new PackOcWorkItemResult();
        if (intent == "detach")
        {
            var byId = existing
                .Where(w => !string.IsNullOrWhiteSpace(w.__dataId))
                .ToDictionary(w => w.__dataId!, StringComparer.Ordinal);
            foreach (var item in detachItems ?? Array.Empty<PackPreviewItemDto>())
            {
                if (string.IsNullOrWhiteSpace(item.WbsId) || !byId.TryGetValue(item.WbsId, out var row))
                    continue;
                if (string.IsNullOrWhiteSpace(row.workItemId))
                    continue;
                if (item.Action == "remove")
                    result.Removed++;
                else
                    result.Kept++;
            }

            return result;
        }

        if (!workspaceWillExist && string.IsNullOrWhiteSpace(workspaceId))
            return result;

        WalkPackWbs(null, pack.Wbs, existing, (_, match, _) =>
        {
            if (match is null || string.IsNullOrWhiteSpace(match.__dataId) || string.IsNullOrWhiteSpace(match.workItemId))
                result.Created++;
            else
                result.Skipped++;
        });

        return result;
    }

    private async Task<PackOcWorkItemResult> EnsurePackOcWorkItemsAsync(
        string projectId,
        string? workspaceId,
        JobPackDefinition pack,
        string token,
        CancellationToken ct)
    {
        var result = new PackOcWorkItemResult();
        if (string.IsNullOrWhiteSpace(workspaceId) || pack.Wbs.Count == 0)
            return result;

        var project = await LoadProjectOrThrowAsync(projectId, token, ct);
        var (typeId, boardId) = await ResolvePackWorkItemTargetsAsync(workspaceId, project, token, ct);
        if (string.IsNullOrWhiteSpace(typeId))
        {
            _logger.LogWarning(
                "Pack {PackCode} skipped work-item seed: no type in workspace {WorkspaceId}",
                pack.Code,
                workspaceId);
            return result;
        }

        var existing = await LoadWbsAsync(projectId, token, ct);
        var bound = 0;
        var visits = new List<(JobPackWbsNode Node, PmWbsRow? Match, PmWbsRow? Parent)>();
        WalkPackWbs(null, pack.Wbs, existing, (node, match, parent) => visits.Add((node, match, parent)));

        foreach (var (_, match, parent) in visits)
        {
            if (match is null || string.IsNullOrWhiteSpace(match.__dataId))
                continue;

            if (string.IsNullOrWhiteSpace(match.workItemId))
            {
                var created = await CreateAndBindPackWorkItemAsync(
                    workspaceId,
                    typeId,
                    boardId,
                    match,
                    parent?.workItemId,
                    existing,
                    token,
                    ct);
                if (!created)
                {
                    result.Skipped++;
                    continue;
                }

                bound++;
                result.Created++;
            }
            else
            {
                result.Skipped++;
            }

            if (!string.IsNullOrWhiteSpace(parent?.workItemId) && !string.IsNullOrWhiteSpace(match.workItemId))
                await EnsurePackWorkItemParentAsync(match.workItemId, parent.workItemId, token, ct);
        }

        if (bound > 0)
            await RecalcProjectProgressAsync(projectId, token, ct);

        return result;
    }

    private async Task<bool> CreateAndBindPackWorkItemAsync(
        string workspaceId,
        string typeId,
        string? boardId,
        PmWbsRow match,
        string? parentWorkItemId,
        List<PmWbsRow> existing,
        string token,
        CancellationToken ct)
    {
        var wbsId = match.__dataId!;
        var created = await _workItems.Value.CreateFromOriginAsync(
            new CreateFromOriginRequest
            {
                WorkspaceId = workspaceId,
                TypeId = typeId,
                Title = (match.name ?? string.Empty).Trim(),
                Description = null,
                BoardId = boardId,
                Origin = new WorkItemOriginInput
                {
                    SourceType = PackWorkItemSourceType,
                    SourceId = wbsId,
                    CorrelationId = wbsId,
                    SourceSystem = PackWorkItemSourceSystem
                }
            },
            ct);

        var workItemId = created.WorkItem.Id;
        if (string.IsNullOrWhiteSpace(workItemId))
            return false;

        var taken = await FindWbsByWorkItemAsync(workItemId, token, ct);
        if (taken is not null && !string.Equals(taken.__dataId, wbsId, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Pack work item {WorkItemId} already bound to WBS {OtherWbs}; skip {WbsId}",
                workItemId,
                taken.__dataId,
                wbsId);
            return false;
        }

        var payload = new Dictionary<string, object?> { ["workItemId"] = workItemId };
        if (!HasWbsChildren(existing, wbsId))
            payload["percentComplete"] = 0;

        await _dg.UpdateAsync(PmDatasets.WbsItems, wbsId, payload, token, ct);
        match.workItemId = workItemId;
        if (payload.ContainsKey("percentComplete"))
            match.percentComplete = 0;

        if (!string.IsNullOrWhiteSpace(parentWorkItemId))
            await EnsurePackWorkItemParentAsync(workItemId, parentWorkItemId, token, ct);

        return true;
    }

    private async Task EnsurePackWorkItemParentAsync(
        string workItemId,
        string parentWorkItemId,
        string token,
        CancellationToken ct)
    {
        if (string.Equals(workItemId, parentWorkItemId, StringComparison.Ordinal))
            return;

        var wi = await _dg.GetByIdAsync<Dictionary<string, object?>>(
            OcDatasets.WorkItems, workItemId, token, ct, expand: false);
        if (wi is null) return;

        var current = WorkItemDataHelper.GetPersonRefId(wi, "parentItemId")
            ?? WorkItemDataHelper.GetString(wi, "parentItemId");
        if (string.Equals(current, parentWorkItemId, StringComparison.Ordinal))
            return;

        await _dg.UpdateAsync(
            OcDatasets.WorkItems,
            workItemId,
            new Dictionary<string, object?> { ["parentItemId"] = parentWorkItemId },
            token,
            ct);
    }

    private async Task<(string? TypeId, string? BoardId)> ResolvePackWorkItemTargetsAsync(
        string workspaceId,
        PmProjectRow project,
        string token,
        CancellationToken ct)
    {
        var typeName = $"{project.code} Task";
        var typeId = await FindFirstIdAsync(
            OcDatasets.WorkItemTypes,
            new Dictionary<string, object?> { ["workspaceId"] = workspaceId, ["name"] = typeName },
            token,
            ct);

        if (string.IsNullOrWhiteSpace(typeId))
        {
            var workspace = await _dg.GetByIdAsync<WorkspaceRecord>(
                OcDatasets.Workspaces, workspaceId, token, ct, expand: false);
            typeId = MetadataRelationHelper.ParseIdList(workspace?.EnabledTypeIds).FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(typeId))
        {
            typeId = await FindFirstIdAsync(
                OcDatasets.WorkItemTypes,
                new Dictionary<string, object?> { ["workspaceId"] = workspaceId },
                token,
                ct);
        }

        var boardId = await FindFirstIdAsync(
            OcDatasets.Boards,
            new Dictionary<string, object?> { ["workspaceId"] = workspaceId, ["name"] = $"{project.code} Board" },
            token,
            ct);

        return (EmptyToNull(typeId), EmptyToNull(boardId));
    }

    private async Task<bool> CanDetachPackWbsAsync(
        PmWbsRow row,
        List<PmWbsRow> remaining,
        string token,
        CancellationToken ct)
    {
        if (HasWbsChildren(remaining, row.__dataId)) return false;
        if ((row.percentComplete ?? 0) >= 0.5) return false;
        if (string.IsNullOrWhiteSpace(row.workItemId)) return true;
        return await IsUnusedPackWorkItemAsync(row, token, ct);
    }

    private async Task<bool> IsUnusedPackWorkItemAsync(PmWbsRow row, string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(row.workItemId)) return true;
        if ((row.percentComplete ?? 0) >= 0.5) return false;

        var wi = await _dg.GetByIdAsync<Dictionary<string, object?>>(
            OcDatasets.WorkItems, row.workItemId, token, ct, expand: false);
        if (wi is null) return true;

        var progress = await ResolveWorkItemProgressAsync(wi, token, ct);
        return progress.Percent < 0.5 && !progress.Closed;
    }

    private async Task<bool> TryReleaseUnusedPackWorkItemAsync(
        PmWbsRow row,
        string token,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(row.workItemId) || string.IsNullOrWhiteSpace(row.__dataId))
            return false;
        if (!await IsUnusedPackWorkItemAsync(row, token, ct))
            return false;

        var workItemId = row.workItemId;
        await _dg.UpdateAsync(PmDatasets.WbsItems, row.__dataId, new Dictionary<string, object?>
        {
            ["workItemId"] = null
        }, token, ct);
        row.workItemId = null;

        try
        {
            await _workItems.Value.DeleteAsync(workItemId, force: true, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pack detach could not delete unused work item {WorkItemId}", workItemId);
            await _dg.UpdateAsync(PmDatasets.WbsItems, row.__dataId, new Dictionary<string, object?>
            {
                ["workItemId"] = workItemId
            }, token, ct);
            row.workItemId = workItemId;
            return false;
        }

        return true;
    }
}
