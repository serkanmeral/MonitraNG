using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Text.Json;
using MngOperations.Application.Contracts.Planning;
using MngOperations.Application.Packs;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Interfaces;
using MngOperations.Application.Models;
using MngOperations.Application.Utilities;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed partial class ProjectPlanningService : IProjectPlanningService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private const string ListQuery = "limit=500&expand=false";

    private readonly IMngDataGatewayClient _dg;
    private readonly IRequestContext _ctx;
    private readonly IMetadataCache _metadata;
    private readonly ILogger<ProjectPlanningService> _logger;

    public ProjectPlanningService(
        IMngDataGatewayClient dg,
        IRequestContext ctx,
        IMetadataCache metadata,
        ILogger<ProjectPlanningService> logger)
    {
        _dg = dg;
        _ctx = ctx;
        _metadata = metadata;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ProjectDto>> ListProjectsAsync(CancellationToken ct = default)
    {
        var token = RequireToken();
        var rows = (await _dg.GetAsync<PmProjectRow>(PmDatasets.Projects, "limit=200&sort=code&expand=false", token, ct)).ToList();
        var wbs = await LoadAllWbsAsync(token, ct);
        return rows.Select(p => ToProjectDto(p, wbs)).ToList();
    }

    public async Task<ProjectDetailDto> GetProjectAsync(string id, CancellationToken ct = default)
    {
        var token = RequireToken();
        var project = await LoadProjectOrThrowAsync(id, token, ct);
        var wbs = await LoadWbsAsync(id, token, ct);
        var deps = await LoadDepsAsync(id, token, ct);
        var dtos = wbs.Select(ToWbsDto).OrderBy(w => w.WbsCode, StringComparer.Ordinal).ThenBy(w => w.SortOrder).ToList();
        await HydrateWorkItemsAsync(dtos, token, ct);
        var decisions = await LoadDecisionsAsync(id, token, ct);
        var gates = await LoadStageGatesAsync(id, token, ct);
        var raid = await LoadRaidItemsAsync(id, token, ct);
        var assignments = await LoadAssignmentDtosAsync(id, dtos, token, ct);
        var budgetLines = await LoadBudgetLineDtosAsync(id, token, ct);
        var acknowledgements = await LoadAcknowledgementDtosAsync(id, token, ct);
        var obligations = await LoadObligationDtosAsync(id, token, ct);
        var auditPacks = await LoadAuditPackDtosAsync(id, token, ct);
        var meetings = await LoadMeetingDtosAsync(id, token, ct);
        var stakeholders = await LoadStakeholderDtosAsync(id, token, ct);
        var processMaps = await LoadProcessMapDtosAsync(id, token, ct);
        return new ProjectDetailDto
        {
            Project = ToProjectDto(project, wbs),
            Wbs = dtos,
            Dependencies = deps.Select(ToDepDto).ToList(),
            Decisions = decisions,
            StageGates = gates,
            RaidItems = raid,
            Assignments = assignments,
            Capacity = BuildCapacity(assignments),
            BudgetLines = budgetLines,
            Budget = BuildBudget(budgetLines),
            Acknowledgements = acknowledgements,
            Obligations = obligations,
            AuditPacks = auditPacks,
            Meetings = meetings,
            Stakeholders = stakeholders,
            ProcessMaps = processMaps
        };
    }

    public Task<IReadOnlyList<JobPackDto>> ListJobPacksAsync(CancellationToken ct = default)
    {
        _ = ct;
        IReadOnlyList<JobPackDto> items = JobPackCatalog.All.Select(JobPackCatalog.ToDto).ToList();
        return Task.FromResult(items);
    }

    public async Task<ProjectDto> CreateProjectAsync(CreateProjectRequest request, CancellationToken ct = default)
    {
        var token = RequireToken();
        var code = (request.Code ?? string.Empty).Trim();
        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(code))
            throw new OperationCoreException("CODE_REQUIRED", "Project code is required.", "Proje kodu zorunludur.", 400);
        if (string.IsNullOrWhiteSpace(name))
            throw new OperationCoreException("NAME_REQUIRED", "Project name is required.", "Proje adı zorunludur.", 400);

        JobPackDefinition? pack = null;
        if (!string.IsNullOrWhiteSpace(request.PackCode))
        {
            pack = JobPackCatalog.Find(request.PackCode);
            if (pack is null)
                throw new OperationCoreException("PACK_UNKNOWN", "Unknown job pack.", "Bilinmeyen iş paketi.", 400);
        }

        await EnsureCodeUniqueAsync(code, null, token, ct);

        var payload = new Dictionary<string, object?>
        {
            ["code"] = code,
            ["name"] = name,
            ["description"] = EmptyToNull(request.Description),
            ["status"] = PmProjectStatus.Normalize(request.Status),
            ["plannedStart"] = request.PlannedStart,
            ["plannedFinish"] = request.PlannedFinish
        };

        try
        {
            var created = await _dg.CreateAsync(PmDatasets.Projects, payload, token, ct);
            var id = ReadId(created);
            if (string.IsNullOrWhiteSpace(id))
                throw new OperationCoreException("CREATE_FAILED", "Project create did not return an id.", "Proje oluşturulamadı.", 500);
            if (pack is not null)
            {
                await ApplyPackWbsAsync(id, pack, token, ct);
                await EnsurePackWorkspaceAsync(id, pack, token, ct);
                await UpsertProjectPackAsync(id, pack, token, ct);
            }
            var row = await LoadProjectOrThrowAsync(id, token, ct);
            var wbs = pack is null
                ? new List<PmWbsRow>()
                : await LoadWbsAsync(id, token, ct);
            return ToProjectDto(row, wbs);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            throw new OperationCoreException("CODE_TAKEN", "Project code already exists.", "Proje kodu zaten var.", 409);
        }
    }

    public async Task<ProjectDto> UpdateProjectAsync(string id, UpdateProjectRequest request, CancellationToken ct = default)
    {
        var token = RequireToken();
        var existing = await LoadProjectOrThrowAsync(id, token, ct);
        var payload = new Dictionary<string, object?>();
        if (request.Name is not null)
        {
            var name = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new OperationCoreException("NAME_REQUIRED", "Project name is required.", "Proje adı zorunludur.", 400);
            payload["name"] = name;
        }
        if (request.Description is not null)
            payload["description"] = EmptyToNull(request.Description);
        if (request.Status is not null)
            payload["status"] = PmProjectStatus.Normalize(request.Status);
        if (request.WorkspaceId is not null)
        {
            var workspaceId = EmptyToNull(request.WorkspaceId);
            if (workspaceId is not null)
                await _metadata.GetWorkspaceAsync(workspaceId, token, ct);
            payload["workspaceId"] = workspaceId;
        }
        if (request.DiFolderId is not null)
            payload["diFolderId"] = EmptyToNull(request.DiFolderId);
        if (request.PlannedStart.HasValue || request.PlannedFinish.HasValue || request.ActualStart.HasValue || request.ActualFinish.HasValue)
        {
            if (request.PlannedStart.HasValue) payload["plannedStart"] = request.PlannedStart;
            if (request.PlannedFinish.HasValue) payload["plannedFinish"] = request.PlannedFinish;
            if (request.ActualStart.HasValue) payload["actualStart"] = request.ActualStart;
            if (request.ActualFinish.HasValue) payload["actualFinish"] = request.ActualFinish;
        }

        if (payload.Count == 0)
        {
            var wbs0 = await LoadWbsAsync(id, token, ct);
            return ToProjectDto(existing, wbs0);
        }

        await _dg.UpdateAsync(PmDatasets.Projects, id, payload, token, ct);
        var updated = await LoadProjectOrThrowAsync(id, token, ct);
        var wbs = await LoadWbsAsync(id, token, ct);
        return ToProjectDto(updated, wbs);
    }

    public async Task DeleteProjectAsync(string id, CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(id, token, ct);
        var wbs = await LoadWbsAsync(id, token, ct);
        var deps = await LoadDepsAsync(id, token, ct);
        var decisions = await LoadDecisionRowsAsync(id, token, ct);
        var packs = await LoadProjectPackRowsAsync(id, token, ct);
        var gates = await LoadStageGateRowsAsync(id, token, ct);
        var raid = await LoadRaidItemRowsAsync(id, token, ct);
        var assignments = await LoadAssignmentRowsAsync(id, token, ct);
        var budgetLines = await LoadBudgetLineRowsAsync(id, token, ct);
        var acks = await LoadAcknowledgementRowsAsync(id, token, ct);
        var obligations = await LoadObligationRowsAsync(id, token, ct);
        var auditPacks = await LoadAuditPackRowsAsync(id, token, ct);
        var meetings = await LoadMeetingRowsAsync(id, token, ct);
        var meetingActions = await LoadMeetingActionRowsAsync(id, token, ct);
        var stakeholders = await LoadStakeholderRowsAsync(id, token, ct);
        var processMaps = await LoadProcessMapRowsAsync(id, token, ct);
        foreach (var d in deps)
        {
            if (!string.IsNullOrWhiteSpace(d.__dataId))
                await _dg.DeleteAsync(PmDatasets.Dependencies, d.__dataId, token, ct);
        }
        foreach (var decision in decisions)
        {
            if (!string.IsNullOrWhiteSpace(decision.__dataId))
                await _dg.DeleteAsync(PmDatasets.Decisions, decision.__dataId, token, ct);
        }
        foreach (var packRow in packs)
        {
            if (!string.IsNullOrWhiteSpace(packRow.__dataId))
                await _dg.DeleteAsync(PmDatasets.ProjectPacks, packRow.__dataId, token, ct);
        }
        foreach (var gate in gates)
        {
            if (!string.IsNullOrWhiteSpace(gate.__dataId))
                await _dg.DeleteAsync(PmDatasets.StageGates, gate.__dataId, token, ct);
        }
        foreach (var item in raid)
        {
            if (!string.IsNullOrWhiteSpace(item.__dataId))
                await _dg.DeleteAsync(PmDatasets.RaidItems, item.__dataId, token, ct);
        }
        foreach (var assignment in assignments)
        {
            if (!string.IsNullOrWhiteSpace(assignment.__dataId))
                await _dg.DeleteAsync(PmDatasets.ResourceAssignments, assignment.__dataId, token, ct);
        }
        foreach (var line in budgetLines)
        {
            if (!string.IsNullOrWhiteSpace(line.__dataId))
                await _dg.DeleteAsync(PmDatasets.BudgetLines, line.__dataId, token, ct);
        }
        foreach (var ack in acks)
        {
            if (!string.IsNullOrWhiteSpace(ack.__dataId))
                await _dg.DeleteAsync(PmDatasets.Acknowledgements, ack.__dataId, token, ct);
        }
        foreach (var obligation in obligations)
        {
            if (!string.IsNullOrWhiteSpace(obligation.__dataId))
                await _dg.DeleteAsync(PmDatasets.Obligations, obligation.__dataId, token, ct);
        }
        foreach (var pack in auditPacks)
        {
            if (!string.IsNullOrWhiteSpace(pack.__dataId))
                await _dg.DeleteAsync(PmDatasets.AuditPacks, pack.__dataId, token, ct);
        }
        foreach (var action in meetingActions)
        {
            if (!string.IsNullOrWhiteSpace(action.__dataId))
                await _dg.DeleteAsync(PmDatasets.MeetingActions, action.__dataId, token, ct);
        }
        foreach (var meeting in meetings)
        {
            if (!string.IsNullOrWhiteSpace(meeting.__dataId))
                await _dg.DeleteAsync(PmDatasets.Meetings, meeting.__dataId, token, ct);
        }
        foreach (var stakeholder in stakeholders)
        {
            if (!string.IsNullOrWhiteSpace(stakeholder.__dataId))
                await _dg.DeleteAsync(PmDatasets.Stakeholders, stakeholder.__dataId, token, ct);
        }
        foreach (var map in processMaps)
        {
            if (!string.IsNullOrWhiteSpace(map.__dataId))
                await _dg.DeleteAsync(PmDatasets.ProcessMaps, map.__dataId, token, ct);
        }
        foreach (var w in wbs)
        {
            if (!string.IsNullOrWhiteSpace(w.__dataId))
                await _dg.DeleteAsync(PmDatasets.WbsItems, w.__dataId, token, ct);
        }
        await _dg.DeleteAsync(PmDatasets.Projects, id, token, ct);
    }

    public async Task<ProjectDetailDto> SetBaselineAsync(string id, SetProjectBaselineRequest request, CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(id, token, ct);
        var wbs = await LoadWbsAsync(id, token, ct);
        foreach (var item in wbs)
        {
            if (string.IsNullOrWhiteSpace(item.__dataId)) continue;
            await _dg.UpdateAsync(PmDatasets.WbsItems, item.__dataId, new Dictionary<string, object?>
            {
                ["baselineStart"] = item.plannedStart,
                ["baselineFinish"] = item.plannedFinish
            }, token, ct);
        }

        await _dg.UpdateAsync(PmDatasets.Projects, id, new Dictionary<string, object?>
        {
            ["baselineSetAt"] = DateTime.UtcNow,
            ["baselineSetBy"] = _ctx.Username,
            ["baselineNote"] = EmptyToNull(request.Note)
        }, token, ct);

        return await GetProjectAsync(id, ct);
    }

    public async Task<WbsItemDto> CreateWbsAsync(string projectId, CreateWbsItemRequest request, CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(projectId, token, ct);
        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new OperationCoreException("NAME_REQUIRED", "WBS name is required.", "WBS adı zorunludur.", 400);

        var parentId = EmptyToNull(request.ParentId);
        if (parentId is not null)
        {
            var parent = await LoadWbsOrThrowAsync(parentId, token, ct);
            if (!string.Equals(parent.projectId, projectId, StringComparison.Ordinal))
                throw new OperationCoreException("PARENT_MISMATCH", "Parent is not in this project.", "Üst kalem bu projede değil.", 400);
        }

        var siblings = (await LoadWbsAsync(projectId, token, ct))
            .Where(w => string.Equals(w.parentId ?? string.Empty, parentId ?? string.Empty, StringComparison.Ordinal))
            .ToList();
        var sort = siblings.Count == 0 ? 10 : siblings.Max(s => s.sortOrder ?? 0) + 10;
        var kind = PmWbsKind.Normalize(request.Kind);
        var finish = kind == PmWbsKind.Milestone
            ? (request.PlannedFinish ?? request.PlannedStart)
            : request.PlannedFinish;

        var payload = new Dictionary<string, object?>
        {
            ["projectId"] = projectId,
            ["parentId"] = parentId,
            ["kind"] = kind,
            ["name"] = name,
            ["sortOrder"] = sort,
            ["plannedStart"] = request.PlannedStart,
            ["plannedFinish"] = finish,
            ["weight"] = request.Weight ?? 1,
            ["percentComplete"] = ClampPercent(request.PercentComplete)
        };

        var created = await _dg.CreateAsync(PmDatasets.WbsItems, payload, token, ct);
        var id = ReadId(created);
        if (string.IsNullOrWhiteSpace(id))
            throw new OperationCoreException("CREATE_FAILED", "WBS create did not return an id.", "WBS oluşturulamadı.", 500);
        await RecalcWbsCodesAsync(projectId, token, ct);
        await RecalcProjectProgressAsync(projectId, token, ct);
        var row = await LoadWbsOrThrowAsync(id, token, ct);
        return ToWbsDto(row);
    }

    public async Task<WbsItemDto> UpdateWbsAsync(string id, UpdateWbsItemRequest request, CancellationToken ct = default)
    {
        var token = RequireToken();
        var existing = await LoadWbsOrThrowAsync(id, token, ct);
        var projectId = existing.projectId!;
        var payload = new Dictionary<string, object?>();
        var parentChanged = false;

        if (request.Name is not null)
        {
            var name = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new OperationCoreException("NAME_REQUIRED", "WBS name is required.", "WBS adı zorunludur.", 400);
            payload["name"] = name;
        }
        if (request.Kind is not null)
            payload["kind"] = PmWbsKind.Normalize(request.Kind);
        if (request.SortOrder.HasValue)
            payload["sortOrder"] = request.SortOrder.Value;
        if (request.PlannedStart.HasValue) payload["plannedStart"] = request.PlannedStart;
        if (request.PlannedFinish.HasValue) payload["plannedFinish"] = request.PlannedFinish;
        if (request.ActualStart.HasValue) payload["actualStart"] = request.ActualStart;
        if (request.ActualFinish.HasValue) payload["actualFinish"] = request.ActualFinish;
        if (request.Weight.HasValue) payload["weight"] = request.Weight.Value;
        if (request.PercentComplete.HasValue && string.IsNullOrWhiteSpace(existing.workItemId))
            payload["percentComplete"] = ClampPercent(request.PercentComplete);

        if (request.ParentId is not null)
        {
            var newParent = EmptyToNull(request.ParentId);
            if (string.Equals(newParent, id, StringComparison.Ordinal))
                throw new OperationCoreException("PARENT_SELF", "WBS cannot be its own parent.", "WBS kendi üstü olamaz.", 400);
            if (newParent is not null)
            {
                var parent = await LoadWbsOrThrowAsync(newParent, token, ct);
                if (!string.Equals(parent.projectId, projectId, StringComparison.Ordinal))
                    throw new OperationCoreException("PARENT_MISMATCH", "Parent is not in this project.", "Üst kalem bu projede değil.", 400);
                var all = await LoadWbsAsync(projectId, token, ct);
                if (IsDescendant(all, id, newParent))
                    throw new OperationCoreException("PARENT_CYCLE", "Cannot move under a descendant.", "Alt kalemin altına taşınamaz.", 400);
            }
            payload["parentId"] = newParent;
            parentChanged = true;
        }

        if (payload.Count > 0)
            await _dg.UpdateAsync(PmDatasets.WbsItems, id, payload, token, ct);
        if (parentChanged || request.SortOrder.HasValue)
            await RecalcWbsCodesAsync(projectId, token, ct);
        if (request.PercentComplete.HasValue || request.Weight.HasValue || parentChanged)
            await RecalcProjectProgressAsync(projectId, token, ct);

        var row = await LoadWbsOrThrowAsync(id, token, ct);
        return ToWbsDto(row);
    }

    public async Task DeleteWbsAsync(string id, CancellationToken ct = default)
    {
        var token = RequireToken();
        var existing = await LoadWbsOrThrowAsync(id, token, ct);
        var projectId = existing.projectId!;
        var all = await LoadWbsAsync(projectId, token, ct);
        var toDelete = CollectSubtreePostOrder(all, id);
        var deleteSet = toDelete.ToHashSet(StringComparer.Ordinal);
        var deps = await LoadDepsAsync(projectId, token, ct);
        foreach (var d in deps)
        {
            if (deleteSet.Contains(d.predecessorId ?? string.Empty) || deleteSet.Contains(d.successorId ?? string.Empty))
            {
                if (!string.IsNullOrWhiteSpace(d.__dataId))
                    await _dg.DeleteAsync(PmDatasets.Dependencies, d.__dataId, token, ct);
            }
        }
        var assignments = await LoadAssignmentRowsAsync(projectId, token, ct);
        foreach (var assignment in assignments)
        {
            if (!string.IsNullOrWhiteSpace(assignment.__dataId) && deleteSet.Contains(assignment.wbsId ?? string.Empty))
                await _dg.DeleteAsync(PmDatasets.ResourceAssignments, assignment.__dataId, token, ct);
        }
        var budgetLines = await LoadBudgetLineRowsAsync(projectId, token, ct);
        foreach (var line in budgetLines)
        {
            if (!string.IsNullOrWhiteSpace(line.__dataId) && deleteSet.Contains(line.wbsId ?? string.Empty))
                await _dg.DeleteAsync(PmDatasets.BudgetLines, line.__dataId, token, ct);
        }
        var acks = await LoadAcknowledgementRowsAsync(projectId, token, ct);
        foreach (var ack in acks)
        {
            if (!string.IsNullOrWhiteSpace(ack.__dataId)
                && !string.IsNullOrWhiteSpace(ack.wbsId)
                && deleteSet.Contains(ack.wbsId))
                await _dg.DeleteAsync(PmDatasets.Acknowledgements, ack.__dataId, token, ct);
        }
        var obligations = await LoadObligationRowsAsync(projectId, token, ct);
        foreach (var obligation in obligations)
        {
            if (!string.IsNullOrWhiteSpace(obligation.__dataId)
                && !string.IsNullOrWhiteSpace(obligation.wbsId)
                && deleteSet.Contains(obligation.wbsId))
                await _dg.DeleteAsync(PmDatasets.Obligations, obligation.__dataId, token, ct);
        }
        var auditPacks = await LoadAuditPackRowsAsync(projectId, token, ct);
        foreach (var pack in auditPacks)
        {
            if (!string.IsNullOrWhiteSpace(pack.__dataId)
                && !string.IsNullOrWhiteSpace(pack.wbsId)
                && deleteSet.Contains(pack.wbsId))
                await _dg.DeleteAsync(PmDatasets.AuditPacks, pack.__dataId, token, ct);
        }
        var meetings = await LoadMeetingRowsAsync(projectId, token, ct);
        var meetingActions = await LoadMeetingActionRowsAsync(projectId, token, ct);
        var deletedMeetingIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var meeting in meetings)
        {
            if (string.IsNullOrWhiteSpace(meeting.__dataId)
                || string.IsNullOrWhiteSpace(meeting.wbsId)
                || !deleteSet.Contains(meeting.wbsId))
                continue;
            deletedMeetingIds.Add(meeting.__dataId);
            await _dg.DeleteAsync(PmDatasets.Meetings, meeting.__dataId, token, ct);
        }
        foreach (var action in meetingActions)
        {
            if (string.IsNullOrWhiteSpace(action.__dataId)) continue;
            var meetingGone = !string.IsNullOrWhiteSpace(action.meetingId) && deletedMeetingIds.Contains(action.meetingId);
            var wbsGone = !string.IsNullOrWhiteSpace(action.wbsId) && deleteSet.Contains(action.wbsId);
            if (meetingGone || wbsGone)
                await _dg.DeleteAsync(PmDatasets.MeetingActions, action.__dataId, token, ct);
        }
        var stakeholders = await LoadStakeholderRowsAsync(projectId, token, ct);
        foreach (var stakeholder in stakeholders)
        {
            if (!string.IsNullOrWhiteSpace(stakeholder.__dataId)
                && !string.IsNullOrWhiteSpace(stakeholder.wbsId)
                && deleteSet.Contains(stakeholder.wbsId))
                await _dg.DeleteAsync(PmDatasets.Stakeholders, stakeholder.__dataId, token, ct);
        }
        var processMaps = await LoadProcessMapRowsAsync(projectId, token, ct);
        foreach (var map in processMaps)
        {
            if (!string.IsNullOrWhiteSpace(map.__dataId)
                && !string.IsNullOrWhiteSpace(map.wbsId)
                && deleteSet.Contains(map.wbsId))
                await _dg.DeleteAsync(PmDatasets.ProcessMaps, map.__dataId, token, ct);
        }
        foreach (var wid in toDelete)
            await _dg.DeleteAsync(PmDatasets.WbsItems, wid, token, ct);

        await RecalcWbsCodesAsync(projectId, token, ct);
        await RecalcProjectProgressAsync(projectId, token, ct);
    }

    public async Task<DependencyDto> CreateDependencyAsync(string projectId, CreateDependencyRequest request, CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(projectId, token, ct);
        var pred = (request.PredecessorId ?? string.Empty).Trim();
        var succ = (request.SuccessorId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(pred) || string.IsNullOrWhiteSpace(succ))
            throw new OperationCoreException("DEP_ENDS_REQUIRED", "Predecessor and successor are required.", "Önceleyen ve izleyen zorunludur.", 400);
        if (string.Equals(pred, succ, StringComparison.Ordinal))
            throw new OperationCoreException("DEP_SELF", "A WBS item cannot depend on itself.", "Kalem kendisine bağlanamaz.", 400);

        var predRow = await LoadWbsOrThrowAsync(pred, token, ct);
        var succRow = await LoadWbsOrThrowAsync(succ, token, ct);
        if (!string.Equals(predRow.projectId, projectId, StringComparison.Ordinal)
            || !string.Equals(succRow.projectId, projectId, StringComparison.Ordinal))
        {
            throw new OperationCoreException("DEP_PROJECT", "Both ends must belong to the project.", "Bağımlılığın iki ucu aynı projede olmalı.", 400);
        }

        var type = PmDependencyType.Normalize(request.Type);
        if (!string.Equals(type, PmDependencyType.FinishToStart, StringComparison.Ordinal))
            throw new OperationCoreException("DEP_TYPE", "Only FS (finish-to-start) is supported in Faz 1.", "Faz 1'de yalnızca FS desteklenir.", 400);

        var existing = await LoadDepsAsync(projectId, token, ct);
        if (WouldCycle(existing, pred, succ))
            throw new OperationCoreException("DEP_CYCLE", "Dependency would create a cycle.", "Bağımlılık döngü oluşturur.", 400);

        var payload = new Dictionary<string, object?>
        {
            ["projectId"] = projectId,
            ["predecessorId"] = pred,
            ["successorId"] = succ,
            ["type"] = type,
            ["lagDays"] = request.LagDays ?? 0
        };

        try
        {
            var created = await _dg.CreateAsync(PmDatasets.Dependencies, payload, token, ct);
            var id = ReadId(created);
            if (string.IsNullOrWhiteSpace(id))
                throw new OperationCoreException("CREATE_FAILED", "Dependency create did not return an id.", "Bağımlılık oluşturulamadı.", 500);
            var row = Map<PmDependencyRow>(created);
            row.__dataId ??= id;
            return ToDepDto(row);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            throw new OperationCoreException("DEP_EXISTS", "This dependency already exists.", "Bu bağımlılık zaten var.", 409);
        }
    }

    public async Task DeleteDependencyAsync(string id, CancellationToken ct = default)
    {
        var token = RequireToken();
        var row = await _dg.GetByIdAsync<PmDependencyRow>(PmDatasets.Dependencies, id, token, ct, expand: false);
        if (row is null)
            throw new OperationCoreException("NOT_FOUND", "Dependency not found.", "Bağımlılık bulunamadı.", 404);
        await _dg.DeleteAsync(PmDatasets.Dependencies, id, token, ct);
    }

    private async Task<PmProjectRow> LoadProjectOrThrowAsync(string id, string token, CancellationToken ct)
    {
        var row = await _dg.GetByIdAsync<PmProjectRow>(PmDatasets.Projects, id, token, ct, expand: false);
        if (row is null || string.IsNullOrWhiteSpace(row.__dataId))
            throw new OperationCoreException("NOT_FOUND", "Project not found.", "Proje bulunamadı.", 404);
        return row;
    }

    private async Task<PmWbsRow> LoadWbsOrThrowAsync(string id, string token, CancellationToken ct)
    {
        var row = await _dg.GetByIdAsync<PmWbsRow>(PmDatasets.WbsItems, id, token, ct, expand: false);
        if (row is null || string.IsNullOrWhiteSpace(row.__dataId))
            throw new OperationCoreException("NOT_FOUND", "WBS item not found.", "WBS kalemi bulunamadı.", 404);
        return row;
    }

    private async Task<List<PmWbsRow>> LoadWbsAsync(string projectId, string token, CancellationToken ct)
    {
        var page = await _dg.QueryPageAsync(
            PmDatasets.WbsItems,
            new Dictionary<string, object?> { ["projectId"] = projectId },
            ListQuery,
            token,
            ct);
        return page.Items.Select(Map<PmWbsRow>).ToList();
    }

    private async Task<List<PmWbsRow>> LoadAllWbsAsync(string token, CancellationToken ct)
    {
        var page = await _dg.QueryPageAsync(
            PmDatasets.WbsItems,
            new Dictionary<string, object?>(),
            "limit=2000&expand=false",
            token,
            ct);
        return page.Items.Select(Map<PmWbsRow>).ToList();
    }

    private async Task<List<PmDependencyRow>> LoadDepsAsync(string projectId, string token, CancellationToken ct)
    {
        var page = await _dg.QueryPageAsync(
            PmDatasets.Dependencies,
            new Dictionary<string, object?> { ["projectId"] = projectId },
            ListQuery,
            token,
            ct);
        return page.Items.Select(Map<PmDependencyRow>).ToList();
    }

    private async Task EnsureCodeUniqueAsync(string code, string? exceptId, string token, CancellationToken ct)
    {
        var page = await _dg.QueryPageAsync(
            PmDatasets.Projects,
            new Dictionary<string, object?> { ["code"] = code },
            "limit=5&expand=false",
            token,
            ct);
        foreach (var row in page.Items)
        {
            var id = ReadId(row);
            if (!string.IsNullOrWhiteSpace(exceptId) && string.Equals(id, exceptId, StringComparison.Ordinal))
                continue;
            if (!string.IsNullOrWhiteSpace(id))
                throw new OperationCoreException("CODE_TAKEN", "Project code already exists.", "Proje kodu zaten var.", 409);
        }
    }

    private async Task RecalcWbsCodesAsync(string projectId, string token, CancellationToken ct)
    {
        var items = await LoadWbsAsync(projectId, token, ct);
        var byParent = items
            .GroupBy(w => w.parentId ?? string.Empty)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.sortOrder ?? 0).ThenBy(x => x.name).ToList());
        var pending = new List<(string Id, string Code)>();

        void Visit(string parentKey, string prefix)
        {
            if (!byParent.TryGetValue(parentKey, out var children)) return;
            var n = 1;
            foreach (var child in children)
            {
                var code = string.IsNullOrEmpty(prefix) ? n.ToString() : $"{prefix}.{n}";
                n++;
                if (!string.Equals(child.wbsCode, code, StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(child.__dataId))
                {
                    pending.Add((child.__dataId, code));
                }
                Visit(child.__dataId ?? string.Empty, code);
            }
        }

        Visit(string.Empty, string.Empty);

        foreach (var (itemId, code) in pending)
        {
            await _dg.UpdateAsync(PmDatasets.WbsItems, itemId, new Dictionary<string, object?> { ["wbsCode"] = code }, token, ct);
        }
    }

    private static bool IsDescendant(IReadOnlyList<PmWbsRow> all, string ancestorId, string nodeId)
    {
        var byId = all.ToDictionary(w => w.__dataId ?? string.Empty);
        var current = nodeId;
        var guard = 0;
        while (!string.IsNullOrEmpty(current) && guard++ < 1000)
        {
            if (string.Equals(current, ancestorId, StringComparison.Ordinal)) return true;
            if (!byId.TryGetValue(current, out var row)) break;
            current = row.parentId ?? string.Empty;
        }
        return false;
    }

    private static List<string> CollectSubtreePostOrder(IReadOnlyList<PmWbsRow> all, string rootId)
    {
        var children = all
            .Where(w => !string.IsNullOrWhiteSpace(w.parentId) && !string.IsNullOrWhiteSpace(w.__dataId))
            .GroupBy(w => w.parentId!)
            .ToDictionary(g => g.Key, g => g.Select(x => x.__dataId!).ToList());
        var ordered = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Walk(string id)
        {
            if (!seen.Add(id)) return;
            if (children.TryGetValue(id, out var kids))
            {
                foreach (var k in kids)
                    Walk(k);
            }
            ordered.Add(id);
        }

        Walk(rootId);
        return ordered;
    }

    private static bool WouldCycle(IReadOnlyList<PmDependencyRow> existing, string pred, string succ)
    {
        // Adding pred -> succ cycles if succ can already reach pred.
        var outgoing = existing
            .GroupBy(d => d.predecessorId ?? string.Empty)
            .ToDictionary(g => g.Key, g => g.Select(x => x.successorId ?? string.Empty).ToList());
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        stack.Push(succ);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            if (!seen.Add(n)) continue;
            if (string.Equals(n, pred, StringComparison.Ordinal)) return true;
            if (outgoing.TryGetValue(n, out var next))
            {
                foreach (var x in next) stack.Push(x);
            }
        }
        return false;
    }

    private static ProjectDto ToProjectDto(PmProjectRow p, IReadOnlyList<PmWbsRow> wbs)
    {
        var id = p.__dataId ?? string.Empty;
        var mine = wbs.Where(w => string.Equals(w.projectId, id, StringComparison.Ordinal)).ToList();
        return new ProjectDto
        {
            Id = id,
            Code = p.code ?? string.Empty,
            Name = p.name ?? string.Empty,
            Description = p.description,
            Status = PmProjectStatus.Normalize(p.status),
            PlannedStart = p.plannedStart,
            PlannedFinish = p.plannedFinish,
            ActualStart = p.actualStart,
            ActualFinish = p.actualFinish,
            BaselineSetAt = p.baselineSetAt,
            BaselineSetBy = p.baselineSetBy,
            BaselineNote = p.baselineNote,
            BaselineDrifted = mine.Any(IsDrifted),
            DiFolderId = p.diFolderId,
            WorkspaceId = p.workspaceId
        };
    }

    private static WbsItemDto ToWbsDto(PmWbsRow w) => new()
    {
        Id = w.__dataId ?? string.Empty,
        ProjectId = w.projectId ?? string.Empty,
        ParentId = w.parentId,
        Kind = PmWbsKind.Normalize(w.kind),
        Name = w.name ?? string.Empty,
        WbsCode = w.wbsCode,
        SortOrder = w.sortOrder ?? 0,
        PlannedStart = w.plannedStart,
        PlannedFinish = w.plannedFinish,
        ActualStart = w.actualStart,
        ActualFinish = w.actualFinish,
        BaselineStart = w.baselineStart,
        BaselineFinish = w.baselineFinish,
        Weight = w.weight ?? 1,
        PercentComplete = w.percentComplete ?? 0,
        WorkItemId = w.workItemId,
        BaselineDrifted = IsDrifted(w)
    };

    private static DependencyDto ToDepDto(PmDependencyRow d) => new()
    {
        Id = d.__dataId ?? string.Empty,
        ProjectId = d.projectId ?? string.Empty,
        PredecessorId = d.predecessorId ?? string.Empty,
        SuccessorId = d.successorId ?? string.Empty,
        Type = PmDependencyType.Normalize(d.type),
        LagDays = (int)(d.lagDays ?? 0)
    };

    private static bool IsDrifted(PmWbsRow w)
    {
        if (w.baselineStart is null && w.baselineFinish is null)
            return false;
        return !SameDay(w.plannedStart, w.baselineStart) || !SameDay(w.plannedFinish, w.baselineFinish);
    }

    private static bool SameDay(DateTime? a, DateTime? b)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        return a.Value.Date == b.Value.Date;
    }

    private static double ClampPercent(double? value)
    {
        if (value is null) return 0;
        if (value < 0) return 0;
        if (value > 100) return 100;
        return value.Value;
    }

    private static string? EmptyToNull(string? value)
    {
        var t = value?.Trim();
        return string.IsNullOrEmpty(t) ? null : t;
    }

    private static T Map<T>(Dictionary<string, object?> row)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var kv in row)
            {
                writer.WritePropertyName(kv.Key);
                WriteJsonValue(writer, kv.Value);
            }
            writer.WriteEndObject();
        }

        return JsonSerializer.Deserialize<T>(buffer.WrittenSpan, JsonOpts)
            ?? throw new OperationCoreException("MAP_FAILED", "Could not map record.", "Kayıt okunamadı.", 500);
    }

    private static void WriteJsonValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case JsonElement el:
                el.WriteTo(writer);
                break;
            default:
                JsonSerializer.Serialize(writer, value, JsonOpts);
                break;
        }
    }

    private static string ReadId(Dictionary<string, object?> row) =>
        WorkItemDataHelper.GetDataId(row);

    private string RequireToken()
    {
        if (string.IsNullOrEmpty(_ctx.BearerToken))
            throw new OperationCoreException("UNAUTHORIZED", "Bearer token is required.", "Bearer token gerekli.", 401);
        return _ctx.BearerToken;
    }
}
