using MngOperations.Application.Contracts.Planning;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Models;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed partial class ProjectPlanningService
{
    public async Task<ProjectObligationsDto> GetObligationsAsync(string projectId, CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(projectId, token, ct);
        var items = await LoadObligationDtosAsync(projectId, token, ct);
        return BuildObligations(items);
    }

    public async Task<ObligationDto> CreateObligationAsync(
        string projectId,
        CreateObligationRequest request,
        CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(projectId, token, ct);
        var title = RequireObligationTitle(request.Title);
        var clauseRef = NormalizeClauseRef(request.ClauseRef);
        var sourceResourceId = NormalizeOptionalId(request.SourceResourceId, "SOURCE_LENGTH", "Source document id is too long.", "Kaynak belge kimliği çok uzun.");
        var evidenceResourceId = NormalizeOptionalId(request.EvidenceResourceId, "EVIDENCE_LENGTH", "Evidence document id is too long.", "Kanıt belge kimliği çok uzun.");
        var workItemId = NormalizeOptionalId(request.WorkItemId, "WORKITEM_LENGTH", "Work item id is too long.", "İş kaydı kimliği çok uzun.");
        var wbsId = await NormalizeOptionalWbsIdAsync(projectId, request.WbsId, token, ct);
        var status = PmObligationStatus.Normalize(request.Status);
        var note = EmptyToNull(request.Note);
        AssertObligationClose(status, evidenceResourceId, note);
        await AssertObligationUniqueAsync(projectId, clauseRef, title, excludeId: null, token, ct);

        var closed = PmObligationStatus.IsClosed(status);
        var payload = new Dictionary<string, object?>
        {
            ["projectId"] = projectId,
            ["title"] = title,
            ["clauseRef"] = EmptyToNull(clauseRef),
            ["sourceResourceId"] = sourceResourceId,
            ["wbsId"] = wbsId,
            ["workItemId"] = workItemId,
            ["evidenceResourceId"] = evidenceResourceId,
            ["status"] = status,
            ["dueDate"] = request.DueDate,
            ["note"] = note,
            ["closedAt"] = closed ? DateTime.UtcNow : null,
            ["closedBy"] = closed ? EmptyToNull(_ctx.Username) : null
        };

        var created = await _dg.CreateAsync(PmDatasets.Obligations, payload, token, ct);
        var id = ReadId(created);
        if (string.IsNullOrWhiteSpace(id))
            throw new OperationCoreException("CREATE_FAILED", "Obligation create did not return an id.", "Yükümlülük kaydı oluşturulamadı.", 500);
        return await LoadObligationDtoAsync(id, token, ct);
    }

    public async Task<ObligationDto> UpdateObligationAsync(string id, UpdateObligationRequest request, CancellationToken ct = default)
    {
        var token = RequireToken();
        var existing = await LoadObligationRowOrThrowAsync(id, token, ct);
        var projectId = existing.projectId!;
        var title = request.Title is not null ? RequireObligationTitle(request.Title) : RequireObligationTitle(existing.title);
        var clauseRef = request.ClauseRef is not null ? NormalizeClauseRef(request.ClauseRef) : NormalizeClauseRef(existing.clauseRef);
        var sourceResourceId = request.SourceResourceId is not null
            ? NormalizeOptionalId(request.SourceResourceId, "SOURCE_LENGTH", "Source document id is too long.", "Kaynak belge kimliği çok uzun.")
            : EmptyToNull(existing.sourceResourceId);
        var evidenceResourceId = request.EvidenceResourceId is not null
            ? NormalizeOptionalId(request.EvidenceResourceId, "EVIDENCE_LENGTH", "Evidence document id is too long.", "Kanıt belge kimliği çok uzun.")
            : EmptyToNull(existing.evidenceResourceId);
        var workItemId = request.WorkItemId is not null
            ? NormalizeOptionalId(request.WorkItemId, "WORKITEM_LENGTH", "Work item id is too long.", "İş kaydı kimliği çok uzun.")
            : EmptyToNull(existing.workItemId);
        var wbsId = request.WbsId is not null
            ? await NormalizeOptionalWbsIdAsync(projectId, request.WbsId, token, ct)
            : EmptyToNull(existing.wbsId);
        var status = PmObligationStatus.Normalize(request.Status ?? existing.status);
        var note = request.Note is not null ? EmptyToNull(request.Note) : EmptyToNull(existing.note);
        AssertObligationClose(status, evidenceResourceId, note);
        await AssertObligationUniqueAsync(projectId, clauseRef, title, id, token, ct);

        var wasClosed = PmObligationStatus.IsClosed(PmObligationStatus.Normalize(existing.status));
        var nowClosed = PmObligationStatus.IsClosed(status);

        var payload = new Dictionary<string, object?>();
        if (request.Title is not null) payload["title"] = title;
        if (request.ClauseRef is not null) payload["clauseRef"] = EmptyToNull(clauseRef);
        if (request.SourceResourceId is not null) payload["sourceResourceId"] = sourceResourceId;
        if (request.WbsId is not null) payload["wbsId"] = wbsId;
        if (request.WorkItemId is not null) payload["workItemId"] = workItemId;
        if (request.EvidenceResourceId is not null) payload["evidenceResourceId"] = evidenceResourceId;
        if (request.Status is not null) payload["status"] = status;
        if (request.DueDate.HasValue) payload["dueDate"] = request.DueDate;
        if (request.Note is not null) payload["note"] = note;

        if (!nowClosed && wasClosed)
        {
            payload["status"] = status;
            payload["closedAt"] = null;
            payload["closedBy"] = null;
        }
        else if (nowClosed && !wasClosed)
        {
            payload["status"] = status;
            payload["closedAt"] = DateTime.UtcNow;
            payload["closedBy"] = EmptyToNull(_ctx.Username);
        }

        if (payload.Count > 0)
            await _dg.UpdateAsync(PmDatasets.Obligations, id, payload, token, ct);
        return await LoadObligationDtoAsync(id, token, ct);
    }

    public async Task DeleteObligationAsync(string id, CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadObligationRowOrThrowAsync(id, token, ct);
        await _dg.DeleteAsync(PmDatasets.Obligations, id, token, ct);
    }

    private async Task<List<ObligationDto>> LoadObligationDtosAsync(string projectId, string token, CancellationToken ct)
    {
        var rows = await LoadObligationRowsAsync(projectId, token, ct);
        return rows
            .Select(ToObligationDto)
            .OrderByDescending(o => o.Open)
            .ThenByDescending(o => o.Overdue)
            .ThenBy(o => o.DueDate)
            .ThenBy(o => o.ClauseRef, StringComparer.OrdinalIgnoreCase)
            .ThenBy(o => o.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<List<PmObligationRow>> LoadObligationRowsAsync(string projectId, string token, CancellationToken ct)
    {
        var page = await _dg.QueryPageAsync(
            PmDatasets.Obligations,
            new Dictionary<string, object?> { ["projectId"] = projectId },
            ListQuery,
            token,
            ct);
        return page.Items.Select(Map<PmObligationRow>).ToList();
    }

    private async Task<PmObligationRow> LoadObligationRowOrThrowAsync(string id, string token, CancellationToken ct)
    {
        var row = await _dg.GetByIdAsync<PmObligationRow>(PmDatasets.Obligations, id, token, ct, expand: false);
        if (row is null || string.IsNullOrWhiteSpace(row.__dataId))
            throw new OperationCoreException("NOT_FOUND", "Obligation not found.", "Yükümlülük kaydı bulunamadı.", 404);
        return row;
    }

    private async Task<ObligationDto> LoadObligationDtoAsync(string id, string token, CancellationToken ct)
    {
        var row = await LoadObligationRowOrThrowAsync(id, token, ct);
        return ToObligationDto(row);
    }

    private async Task AssertObligationUniqueAsync(
        string projectId,
        string clauseRef,
        string title,
        string? excludeId,
        string token,
        CancellationToken ct)
    {
        var rows = await LoadObligationRowsAsync(projectId, token, ct);
        foreach (var row in rows)
        {
            if (!string.IsNullOrWhiteSpace(excludeId) && string.Equals(row.__dataId, excludeId, StringComparison.Ordinal))
                continue;
            if (!string.Equals(NormalizeClauseRef(row.clauseRef), clauseRef, StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals((row.title ?? string.Empty).Trim(), title, StringComparison.OrdinalIgnoreCase))
                throw new OperationCoreException(
                    "OBLIGATION_EXISTS",
                    "This obligation already exists on the project.",
                    "Bu yükümlülük bu projede zaten var.",
                    409);
        }
    }

    private static ObligationDto ToObligationDto(PmObligationRow row)
    {
        var status = PmObligationStatus.Normalize(row.status);
        var open = PmObligationStatus.IsOpen(status);
        var due = row.dueDate?.ToUniversalTime().Date;
        var overdue = open && due is not null && due.Value < DateTime.UtcNow.Date;
        var workItemId = EmptyToNull(row.workItemId);
        var evidenceId = EmptyToNull(row.evidenceResourceId);
        return new ObligationDto
        {
            Id = row.__dataId ?? string.Empty,
            ProjectId = row.projectId ?? string.Empty,
            Title = (row.title ?? string.Empty).Trim(),
            ClauseRef = EmptyToNull(NormalizeClauseRef(row.clauseRef)),
            SourceResourceId = EmptyToNull(row.sourceResourceId),
            WbsId = EmptyToNull(row.wbsId),
            WorkItemId = workItemId,
            EvidenceResourceId = evidenceId,
            Status = status,
            DueDate = row.dueDate,
            Note = EmptyToNull(row.note),
            ClosedAt = row.closedAt,
            ClosedBy = EmptyToNull(row.closedBy),
            Open = open,
            Overdue = overdue,
            Unbound = open && workItemId is null,
            MissingEvidence = open && evidenceId is null
        };
    }

    internal static ProjectObligationsDto BuildObligations(IReadOnlyList<ObligationDto> items)
    {
        return new ProjectObligationsDto
        {
            OpenCount = items.Count(o => o.Open),
            OverdueCount = items.Count(o => o.Overdue),
            UnboundCount = items.Count(o => o.Unbound),
            Items = items
        };
    }

    private static string RequireObligationTitle(string? value)
    {
        var title = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title))
            throw new OperationCoreException("TITLE_REQUIRED", "Obligation title is required.", "Yükümlülük metni zorunludur.", 400);
        return title;
    }

    private static string NormalizeClauseRef(string? value) => (value ?? string.Empty).Trim();

    private static string? NormalizeOptionalId(string? value, string code, string en, string tr)
    {
        var id = EmptyToNull(value);
        if (id is null) return null;
        if (id.Length > 64)
            throw new OperationCoreException(code, en, tr, 400);
        return id;
    }

    private static void AssertObligationClose(string status, string? evidenceResourceId, string? note)
    {
        if (string.Equals(status, PmObligationStatus.Satisfied, StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(evidenceResourceId))
        {
            throw new OperationCoreException(
                "OBLIGATION_EVIDENCE",
                "Evidence document is required to mark the obligation satisfied.",
                "Karşılandı işaretlemek için kanıt belgesi zorunludur.",
                400);
        }

        if (string.Equals(status, PmObligationStatus.Waived, StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(note))
        {
            throw new OperationCoreException(
                "OBLIGATION_NOTE",
                "A note is required to waive an obligation.",
                "Feragat için not zorunludur.",
                400);
        }
    }
}
