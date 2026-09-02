using MngOperations.Application.Contracts.Planning;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Models;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed partial class ProjectPlanningService
{
    public async Task<StageGateDto> CreateStageGateAsync(string projectId, CreateStageGateRequest request, CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(projectId, token, ct);
        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new OperationCoreException("NAME_REQUIRED", "Gate name is required.", "Kapı adı zorunludur.", 400);

        var criteria = NormalizeLabels(request.Criteria);
        var satisfied = FilterSatisfied(criteria, request.Satisfied);
        var status = PmStageGateStatus.Normalize(request.Status);
        var note = EmptyToNull(request.Note);
        AssertGateTransition(status, criteria, satisfied, note);

        var wbsId = await NormalizeOptionalWbsIdAsync(projectId, request.WbsId, token, ct);
        var decided = IsClosedGate(status);
        var payload = new Dictionary<string, object?>
        {
            ["projectId"] = projectId,
            ["name"] = name,
            ["wbsId"] = wbsId,
            ["sortOrder"] = request.SortOrder ?? 10,
            ["status"] = status,
            ["criteria"] = criteria,
            ["satisfied"] = satisfied,
            ["note"] = note,
            ["decidedAt"] = decided ? DateTime.UtcNow : null,
            ["decidedBy"] = decided ? EmptyToNull(_ctx.Username) : null,
            ["resourceIds"] = CleanIds(request.ResourceIds),
            ["decisionId"] = EmptyToNull(request.DecisionId)
        };

        var created = await _dg.CreateAsync(PmDatasets.StageGates, payload, token, ct);
        var id = ReadId(created);
        if (string.IsNullOrWhiteSpace(id))
            throw new OperationCoreException("CREATE_FAILED", "Stage gate create did not return an id.", "Aşama kapısı oluşturulamadı.", 500);
        return await LoadStageGateDtoAsync(id, token, ct);
    }

    public async Task<StageGateDto> UpdateStageGateAsync(string id, UpdateStageGateRequest request, CancellationToken ct = default)
    {
        var token = RequireToken();
        var existing = await LoadStageGateRowOrThrowAsync(id, token, ct);
        var projectId = existing.projectId!;
        var criteria = request.Criteria is not null
            ? NormalizeLabels(request.Criteria)
            : NormalizeLabels(existing.criteria);
        var satisfiedSource = request.Satisfied ?? existing.satisfied;
        var satisfied = FilterSatisfied(criteria, satisfiedSource);
        var status = request.Status is not null
            ? PmStageGateStatus.Normalize(request.Status)
            : PmStageGateStatus.Normalize(existing.status);
        var note = request.Note is not null ? EmptyToNull(request.Note) : existing.note;
        AssertGateTransition(status, criteria, satisfied, note);

        var payload = new Dictionary<string, object?>();
        if (request.Name is not null)
        {
            var name = request.Name.Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new OperationCoreException("NAME_REQUIRED", "Gate name is required.", "Kapı adı zorunludur.", 400);
            payload["name"] = name;
        }
        if (request.WbsId is not null)
            payload["wbsId"] = await NormalizeOptionalWbsIdAsync(projectId, request.WbsId, token, ct);
        if (request.SortOrder.HasValue) payload["sortOrder"] = request.SortOrder.Value;
        if (request.Status is not null) payload["status"] = status;
        if (request.Criteria is not null) payload["criteria"] = criteria;
        if (request.Satisfied is not null || request.Criteria is not null) payload["satisfied"] = satisfied;
        if (request.Note is not null) payload["note"] = note;
        if (request.ResourceIds is not null) payload["resourceIds"] = CleanIds(request.ResourceIds);
        if (request.DecisionId is not null) payload["decisionId"] = EmptyToNull(request.DecisionId);

        var wasClosed = IsClosedGate(existing.status);
        var nowClosed = IsClosedGate(status);
        if (nowClosed && (!wasClosed || request.Status is not null))
        {
            payload["decidedAt"] = DateTime.UtcNow;
            payload["decidedBy"] = EmptyToNull(_ctx.Username);
        }
        else if (!nowClosed && wasClosed && request.Status is not null)
        {
            payload["decidedAt"] = null;
            payload["decidedBy"] = null;
        }

        if (payload.Count > 0)
            await _dg.UpdateAsync(PmDatasets.StageGates, id, payload, token, ct);
        return await LoadStageGateDtoAsync(id, token, ct);
    }

    public async Task DeleteStageGateAsync(string id, CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadStageGateRowOrThrowAsync(id, token, ct);
        await _dg.DeleteAsync(PmDatasets.StageGates, id, token, ct);
    }

    private async Task<List<StageGateDto>> LoadStageGatesAsync(string projectId, string token, CancellationToken ct)
    {
        var rows = await LoadStageGateRowsAsync(projectId, token, ct);
        return rows
            .Select(ToStageGateDto)
            .OrderBy(g => g.SortOrder)
            .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<List<PmStageGateRow>> LoadStageGateRowsAsync(string projectId, string token, CancellationToken ct)
    {
        var page = await _dg.QueryPageAsync(
            PmDatasets.StageGates,
            new Dictionary<string, object?> { ["projectId"] = projectId },
            ListQuery,
            token,
            ct);
        return page.Items.Select(Map<PmStageGateRow>).ToList();
    }

    private async Task<PmStageGateRow> LoadStageGateRowOrThrowAsync(string id, string token, CancellationToken ct)
    {
        var row = await _dg.GetByIdAsync<PmStageGateRow>(PmDatasets.StageGates, id, token, ct, expand: false);
        if (row is null || string.IsNullOrWhiteSpace(row.__dataId))
            throw new OperationCoreException("NOT_FOUND", "Stage gate not found.", "Aşama kapısı bulunamadı.", 404);
        return row;
    }

    private async Task<StageGateDto> LoadStageGateDtoAsync(string id, string token, CancellationToken ct)
    {
        var row = await LoadStageGateRowOrThrowAsync(id, token, ct);
        return ToStageGateDto(row);
    }

    private async Task<string?> NormalizeOptionalWbsIdAsync(
        string projectId,
        string? wbsId,
        string token,
        CancellationToken ct)
    {
        var id = EmptyToNull(wbsId);
        if (id is null) return null;
        var ids = await NormalizeWbsIdsAsync(projectId, new[] { id }, token, ct);
        return ids.Count == 0 ? null : ids[0];
    }

    private static StageGateDto ToStageGateDto(PmStageGateRow row)
    {
        var criteria = NormalizeLabels(row.criteria);
        return new StageGateDto
        {
            Id = row.__dataId ?? string.Empty,
            ProjectId = row.projectId ?? string.Empty,
            Name = row.name ?? string.Empty,
            WbsId = EmptyToNull(row.wbsId),
            SortOrder = row.sortOrder ?? 0,
            Status = PmStageGateStatus.Normalize(row.status),
            Criteria = criteria,
            Satisfied = FilterSatisfied(criteria, row.satisfied),
            Note = row.note,
            DecidedAt = row.decidedAt,
            DecidedBy = row.decidedBy,
            ResourceIds = row.resourceIds ?? new List<string>(),
            DecisionId = EmptyToNull(row.decisionId)
        };
    }

    private static void AssertGateTransition(
        string status,
        IReadOnlyList<string> criteria,
        IReadOnlyList<string> satisfied,
        string? note)
    {
        if (string.Equals(status, PmStageGateStatus.Passed, StringComparison.Ordinal)
            && !CriteriaMet(criteria, satisfied))
        {
            throw new OperationCoreException(
                "GATE_CRITERIA",
                "All checklist items must be met before passing the gate.",
                "Kapıdan geçmek için tüm kriterler işaretlenmeli.",
                400);
        }

        if ((string.Equals(status, PmStageGateStatus.Failed, StringComparison.Ordinal)
             || string.Equals(status, PmStageGateStatus.Waived, StringComparison.Ordinal))
            && string.IsNullOrWhiteSpace(note))
        {
            throw new OperationCoreException(
                "GATE_NOTE",
                "A note is required to fail or waive a gate.",
                "Red veya feragat için not zorunludur.",
                400);
        }
    }

    private static bool IsClosedGate(string? status)
    {
        var s = PmStageGateStatus.Normalize(status);
        return s is PmStageGateStatus.Passed or PmStageGateStatus.Failed or PmStageGateStatus.Waived;
    }

    private static bool CriteriaMet(IReadOnlyList<string> criteria, IReadOnlyList<string> satisfied)
    {
        if (criteria.Count == 0) return true;
        return criteria.All(c =>
            satisfied.Any(s => string.Equals(s, c, StringComparison.OrdinalIgnoreCase)));
    }

    private static List<string> NormalizeLabels(IReadOnlyList<string>? labels)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();
        foreach (var raw in labels ?? Array.Empty<string>())
        {
            var label = raw?.Trim();
            if (string.IsNullOrWhiteSpace(label) || !seen.Add(label)) continue;
            list.Add(label);
        }
        return list;
    }

    private static List<string> FilterSatisfied(IReadOnlyList<string> criteria, IReadOnlyList<string>? satisfied)
    {
        if (criteria.Count == 0) return [];
        var wanted = criteria.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return NormalizeLabels(satisfied).Where(wanted.Contains).ToList();
    }
}
