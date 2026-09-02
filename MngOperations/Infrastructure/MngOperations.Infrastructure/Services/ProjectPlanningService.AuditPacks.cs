using MngOperations.Application.Contracts.Planning;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Models;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed partial class ProjectPlanningService
{
    public async Task<ProjectAuditPacksDto> GetAuditPacksAsync(string projectId, CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(projectId, token, ct);
        var items = await LoadAuditPackDtosAsync(projectId, token, ct);
        return BuildAuditPacks(items);
    }

    public async Task<AuditPackDto> CreateAuditPackAsync(
        string projectId,
        CreateAuditPackRequest request,
        CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(projectId, token, ct);
        var name = RequireAuditPackName(request.Name);
        var kind = PmAuditPackKind.Normalize(request.Kind);
        var wbsId = await NormalizeOptionalWbsIdAsync(projectId, request.WbsId, token, ct);
        var status = PmAuditPackStatus.Normalize(request.Status);
        var resourceIds = CleanIds(request.ResourceIds);
        var note = EmptyToNull(request.Note);
        AssertAuditPackClose(status, resourceIds, note);
        await AssertAuditPackUniqueAsync(projectId, name, excludeId: null, token, ct);

        var closed = PmAuditPackStatus.IsClosed(status);
        var payload = new Dictionary<string, object?>
        {
            ["projectId"] = projectId,
            ["name"] = name,
            ["kind"] = kind,
            ["wbsId"] = wbsId,
            ["status"] = status,
            ["dueDate"] = request.DueDate,
            ["resourceIds"] = resourceIds,
            ["recipient"] = EmptyToNull(request.Recipient),
            ["note"] = note,
            ["issuedAt"] = closed ? DateTime.UtcNow : null,
            ["issuedBy"] = closed ? EmptyToNull(_ctx.Username) : null
        };

        var created = await _dg.CreateAsync(PmDatasets.AuditPacks, payload, token, ct);
        var id = ReadId(created);
        if (string.IsNullOrWhiteSpace(id))
            throw new OperationCoreException("CREATE_FAILED", "Audit pack create did not return an id.", "Denetim paketi oluşturulamadı.", 500);
        return await LoadAuditPackDtoAsync(id, token, ct);
    }

    public async Task<AuditPackDto> UpdateAuditPackAsync(string id, UpdateAuditPackRequest request, CancellationToken ct = default)
    {
        var token = RequireToken();
        var existing = await LoadAuditPackRowOrThrowAsync(id, token, ct);
        var projectId = existing.projectId!;
        var name = request.Name is not null ? RequireAuditPackName(request.Name) : RequireAuditPackName(existing.name);
        var kind = PmAuditPackKind.Normalize(request.Kind ?? existing.kind);
        var wbsId = request.WbsId is not null
            ? await NormalizeOptionalWbsIdAsync(projectId, request.WbsId, token, ct)
            : EmptyToNull(existing.wbsId);
        var status = PmAuditPackStatus.Normalize(request.Status ?? existing.status);
        var resourceIds = request.ResourceIds is not null ? CleanIds(request.ResourceIds) : CleanIds(existing.resourceIds);
        var note = request.Note is not null ? EmptyToNull(request.Note) : EmptyToNull(existing.note);
        AssertAuditPackClose(status, resourceIds, note);
        await AssertAuditPackUniqueAsync(projectId, name, id, token, ct);

        var wasClosed = PmAuditPackStatus.IsClosed(PmAuditPackStatus.Normalize(existing.status));
        var nowClosed = PmAuditPackStatus.IsClosed(status);

        var payload = new Dictionary<string, object?>();
        if (request.Name is not null) payload["name"] = name;
        if (request.Kind is not null) payload["kind"] = kind;
        if (request.WbsId is not null) payload["wbsId"] = wbsId;
        if (request.Status is not null) payload["status"] = status;
        if (request.DueDate.HasValue) payload["dueDate"] = request.DueDate;
        if (request.ResourceIds is not null) payload["resourceIds"] = resourceIds;
        if (request.Recipient is not null) payload["recipient"] = EmptyToNull(request.Recipient);
        if (request.Note is not null) payload["note"] = note;

        if (!nowClosed && wasClosed)
        {
            payload["status"] = status;
            payload["issuedAt"] = null;
            payload["issuedBy"] = null;
        }
        else if (nowClosed && !wasClosed)
        {
            payload["status"] = status;
            payload["issuedAt"] = DateTime.UtcNow;
            payload["issuedBy"] = EmptyToNull(_ctx.Username);
        }

        if (payload.Count > 0)
            await _dg.UpdateAsync(PmDatasets.AuditPacks, id, payload, token, ct);
        return await LoadAuditPackDtoAsync(id, token, ct);
    }

    public async Task DeleteAuditPackAsync(string id, CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadAuditPackRowOrThrowAsync(id, token, ct);
        await _dg.DeleteAsync(PmDatasets.AuditPacks, id, token, ct);
    }

    private async Task<List<AuditPackDto>> LoadAuditPackDtosAsync(string projectId, string token, CancellationToken ct)
    {
        var rows = await LoadAuditPackRowsAsync(projectId, token, ct);
        return rows
            .Select(ToAuditPackDto)
            .OrderByDescending(p => p.Open)
            .ThenByDescending(p => p.Incomplete)
            .ThenByDescending(p => p.Overdue)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<List<PmAuditPackRow>> LoadAuditPackRowsAsync(string projectId, string token, CancellationToken ct)
    {
        var page = await _dg.QueryPageAsync(
            PmDatasets.AuditPacks,
            new Dictionary<string, object?> { ["projectId"] = projectId },
            ListQuery,
            token,
            ct);
        return page.Items.Select(Map<PmAuditPackRow>).ToList();
    }

    private async Task<PmAuditPackRow> LoadAuditPackRowOrThrowAsync(string id, string token, CancellationToken ct)
    {
        var row = await _dg.GetByIdAsync<PmAuditPackRow>(PmDatasets.AuditPacks, id, token, ct, expand: false);
        if (row is null || string.IsNullOrWhiteSpace(row.__dataId))
            throw new OperationCoreException("NOT_FOUND", "Audit pack not found.", "Denetim paketi bulunamadı.", 404);
        return row;
    }

    private async Task<AuditPackDto> LoadAuditPackDtoAsync(string id, string token, CancellationToken ct)
    {
        var row = await LoadAuditPackRowOrThrowAsync(id, token, ct);
        return ToAuditPackDto(row);
    }

    private async Task AssertAuditPackUniqueAsync(
        string projectId,
        string name,
        string? excludeId,
        string token,
        CancellationToken ct)
    {
        var rows = await LoadAuditPackRowsAsync(projectId, token, ct);
        foreach (var row in rows)
        {
            if (!string.IsNullOrWhiteSpace(excludeId) && string.Equals(row.__dataId, excludeId, StringComparison.Ordinal))
                continue;
            if (string.Equals((row.name ?? string.Empty).Trim(), name, StringComparison.OrdinalIgnoreCase))
                throw new OperationCoreException(
                    "AUDIT_PACK_EXISTS",
                    "This audit pack already exists on the project.",
                    "Bu denetim paketi bu projede zaten var.",
                    409);
        }
    }

    private static AuditPackDto ToAuditPackDto(PmAuditPackRow row)
    {
        var status = PmAuditPackStatus.Normalize(row.status);
        var open = PmAuditPackStatus.IsOpen(status);
        var resources = CleanIds(row.resourceIds);
        var due = row.dueDate?.ToUniversalTime().Date;
        var overdue = open && due is not null && due.Value < DateTime.UtcNow.Date;
        return new AuditPackDto
        {
            Id = row.__dataId ?? string.Empty,
            ProjectId = row.projectId ?? string.Empty,
            Name = (row.name ?? string.Empty).Trim(),
            Kind = PmAuditPackKind.Normalize(row.kind),
            WbsId = EmptyToNull(row.wbsId),
            Status = status,
            DueDate = row.dueDate,
            ResourceIds = resources,
            Recipient = EmptyToNull(row.recipient),
            Note = EmptyToNull(row.note),
            IssuedAt = row.issuedAt,
            IssuedBy = EmptyToNull(row.issuedBy),
            ItemCount = resources.Count,
            Open = open,
            Incomplete = open && resources.Count == 0,
            Overdue = overdue
        };
    }

    internal static ProjectAuditPacksDto BuildAuditPacks(IReadOnlyList<AuditPackDto> items)
    {
        return new ProjectAuditPacksDto
        {
            OpenCount = items.Count(p => p.Open),
            IncompleteCount = items.Count(p => p.Incomplete),
            OverdueCount = items.Count(p => p.Overdue),
            Items = items
        };
    }

    private static string RequireAuditPackName(string? value)
    {
        var name = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new OperationCoreException("NAME_REQUIRED", "Audit pack name is required.", "Paket adı zorunludur.", 400);
        return name;
    }

    private static void AssertAuditPackClose(string status, IReadOnlyList<string> resourceIds, string? note)
    {
        if (string.Equals(status, PmAuditPackStatus.Issued, StringComparison.Ordinal) && resourceIds.Count == 0)
        {
            throw new OperationCoreException(
                "AUDIT_PACK_EVIDENCE",
                "At least one evidence document is required to issue the pack.",
                "Paketi teslim etmek için en az bir kanıt belgesi gerekir.",
                400);
        }

        if (string.Equals(status, PmAuditPackStatus.Withdrawn, StringComparison.Ordinal) && string.IsNullOrWhiteSpace(note))
        {
            throw new OperationCoreException(
                "AUDIT_PACK_NOTE",
                "A note is required to withdraw an audit pack.",
                "Geri çekmek için not zorunludur.",
                400);
        }
    }
}
