using MngOperations.Application.Contracts.Planning;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Models;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed partial class ProjectPlanningService
{
    public async Task<ProjectStakeholdersDto> GetStakeholdersAsync(string projectId, CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(projectId, token, ct);
        var items = await LoadStakeholderDtosAsync(projectId, token, ct);
        return BuildStakeholders(items);
    }

    public async Task<StakeholderDto> CreateStakeholderAsync(
        string projectId,
        CreateStakeholderRequest request,
        CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(projectId, token, ct);
        var name = RequireStakeholderName(request.Name);
        var kind = PmStakeholderKind.Normalize(request.Kind);
        var wbsId = await NormalizeOptionalWbsIdAsync(projectId, request.WbsId, token, ct);
        var status = PmStakeholderStatus.Normalize(request.Status);
        var resourceIds = CleanIds(request.ResourceIds);
        var note = EmptyToNull(request.Note);
        var email = NormalizeEmail(request.Email);
        AssertStakeholderRevoke(status, note);
        await AssertStakeholderUniqueAsync(projectId, name, excludeId: null, token, ct);

        var closed = PmStakeholderStatus.IsClosed(status);
        var payload = new Dictionary<string, object?>
        {
            ["projectId"] = projectId,
            ["name"] = name,
            ["organization"] = EmptyToNull(request.Organization),
            ["kind"] = kind,
            ["email"] = email,
            ["wbsId"] = wbsId,
            ["status"] = status,
            ["accessUntil"] = request.AccessUntil,
            ["resourceIds"] = resourceIds,
            ["note"] = note,
            ["revokedAt"] = closed ? DateTime.UtcNow : null,
            ["revokedBy"] = closed ? EmptyToNull(_ctx.Username) : null
        };

        var created = await _dg.CreateAsync(PmDatasets.Stakeholders, payload, token, ct);
        var id = ReadId(created);
        if (string.IsNullOrWhiteSpace(id))
            throw new OperationCoreException("CREATE_FAILED", "Stakeholder create did not return an id.", "Paydaş kaydı oluşturulamadı.", 500);
        return await LoadStakeholderDtoAsync(id, token, ct);
    }

    public async Task<StakeholderDto> UpdateStakeholderAsync(string id, UpdateStakeholderRequest request, CancellationToken ct = default)
    {
        var token = RequireToken();
        var existing = await LoadStakeholderRowOrThrowAsync(id, token, ct);
        var projectId = existing.projectId!;
        var name = request.Name is not null ? RequireStakeholderName(request.Name) : RequireStakeholderName(existing.name);
        var kind = PmStakeholderKind.Normalize(request.Kind ?? existing.kind);
        var wbsId = request.WbsId is not null
            ? await NormalizeOptionalWbsIdAsync(projectId, request.WbsId, token, ct)
            : EmptyToNull(existing.wbsId);
        var status = PmStakeholderStatus.Normalize(request.Status ?? existing.status);
        var resourceIds = request.ResourceIds is not null ? CleanIds(request.ResourceIds) : CleanIds(existing.resourceIds);
        var note = request.Note is not null ? EmptyToNull(request.Note) : EmptyToNull(existing.note);
        var email = request.Email is not null ? NormalizeEmail(request.Email) : EmptyToNull(existing.email);
        AssertStakeholderRevoke(status, note);
        await AssertStakeholderUniqueAsync(projectId, name, id, token, ct);

        var wasClosed = PmStakeholderStatus.IsClosed(PmStakeholderStatus.Normalize(existing.status));
        var nowClosed = PmStakeholderStatus.IsClosed(status);

        var payload = new Dictionary<string, object?>();
        if (request.Name is not null) payload["name"] = name;
        if (request.Organization is not null) payload["organization"] = EmptyToNull(request.Organization);
        if (request.Kind is not null) payload["kind"] = kind;
        if (request.Email is not null) payload["email"] = email;
        if (request.WbsId is not null) payload["wbsId"] = wbsId;
        if (request.Status is not null) payload["status"] = status;
        if (request.AccessUntil.HasValue) payload["accessUntil"] = request.AccessUntil;
        if (request.ResourceIds is not null) payload["resourceIds"] = resourceIds;
        if (request.Note is not null) payload["note"] = note;

        if (!nowClosed && wasClosed)
        {
            payload["status"] = status;
            payload["revokedAt"] = null;
            payload["revokedBy"] = null;
        }
        else if (nowClosed && !wasClosed)
        {
            payload["status"] = status;
            payload["revokedAt"] = DateTime.UtcNow;
            payload["revokedBy"] = EmptyToNull(_ctx.Username);
        }

        if (payload.Count > 0)
            await _dg.UpdateAsync(PmDatasets.Stakeholders, id, payload, token, ct);
        return await LoadStakeholderDtoAsync(id, token, ct);
    }

    public async Task DeleteStakeholderAsync(string id, CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadStakeholderRowOrThrowAsync(id, token, ct);
        await _dg.DeleteAsync(PmDatasets.Stakeholders, id, token, ct);
    }

    private async Task<List<StakeholderDto>> LoadStakeholderDtosAsync(string projectId, string token, CancellationToken ct)
    {
        var rows = await LoadStakeholderRowsAsync(projectId, token, ct);
        return rows
            .Select(ToStakeholderDto)
            .OrderByDescending(p => p.Open)
            .ThenByDescending(p => p.Incomplete)
            .ThenByDescending(p => p.Overdue)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<List<PmStakeholderRow>> LoadStakeholderRowsAsync(string projectId, string token, CancellationToken ct)
    {
        var page = await _dg.QueryPageAsync(
            PmDatasets.Stakeholders,
            new Dictionary<string, object?> { ["projectId"] = projectId },
            ListQuery,
            token,
            ct);
        return page.Items.Select(Map<PmStakeholderRow>).ToList();
    }

    private async Task<PmStakeholderRow> LoadStakeholderRowOrThrowAsync(string id, string token, CancellationToken ct)
    {
        var row = await _dg.GetByIdAsync<PmStakeholderRow>(PmDatasets.Stakeholders, id, token, ct, expand: false);
        if (row is null || string.IsNullOrWhiteSpace(row.__dataId))
            throw new OperationCoreException("NOT_FOUND", "Stakeholder not found.", "Paydaş bulunamadı.", 404);
        return row;
    }

    private async Task<StakeholderDto> LoadStakeholderDtoAsync(string id, string token, CancellationToken ct)
    {
        var row = await LoadStakeholderRowOrThrowAsync(id, token, ct);
        return ToStakeholderDto(row);
    }

    private async Task AssertStakeholderUniqueAsync(
        string projectId,
        string name,
        string? excludeId,
        string token,
        CancellationToken ct)
    {
        var rows = await LoadStakeholderRowsAsync(projectId, token, ct);
        foreach (var row in rows)
        {
            if (!string.IsNullOrWhiteSpace(excludeId) && string.Equals(row.__dataId, excludeId, StringComparison.Ordinal))
                continue;
            if (string.Equals((row.name ?? string.Empty).Trim(), name, StringComparison.OrdinalIgnoreCase))
                throw new OperationCoreException(
                    "STAKEHOLDER_EXISTS",
                    "This stakeholder already exists on the project.",
                    "Bu paydaş bu projede zaten var.",
                    409);
        }
    }

    private static StakeholderDto ToStakeholderDto(PmStakeholderRow row)
    {
        var status = PmStakeholderStatus.Normalize(row.status);
        var open = PmStakeholderStatus.IsOpen(status);
        var resources = CleanIds(row.resourceIds);
        var until = row.accessUntil?.ToUniversalTime().Date;
        return new StakeholderDto
        {
            Id = row.__dataId ?? string.Empty,
            ProjectId = row.projectId ?? string.Empty,
            Name = (row.name ?? string.Empty).Trim(),
            Organization = EmptyToNull(row.organization),
            Kind = PmStakeholderKind.Normalize(row.kind),
            Email = EmptyToNull(row.email),
            WbsId = EmptyToNull(row.wbsId),
            Status = status,
            AccessUntil = row.accessUntil,
            ResourceIds = resources,
            Note = EmptyToNull(row.note),
            RevokedAt = row.revokedAt,
            RevokedBy = EmptyToNull(row.revokedBy),
            ItemCount = resources.Count,
            Open = open,
            Incomplete = open && resources.Count == 0,
            Overdue = open && until is not null && until.Value < DateTime.UtcNow.Date
        };
    }

    internal static ProjectStakeholdersDto BuildStakeholders(IReadOnlyList<StakeholderDto> items)
    {
        return new ProjectStakeholdersDto
        {
            OpenCount = items.Count(p => p.Open),
            IncompleteCount = items.Count(p => p.Incomplete),
            OverdueCount = items.Count(p => p.Overdue),
            Items = items
        };
    }

    private static string RequireStakeholderName(string? value)
    {
        var name = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new OperationCoreException("NAME_REQUIRED", "Stakeholder name is required.", "Paydaş adı zorunludur.", 400);
        return name;
    }

    private static string? NormalizeEmail(string? value)
    {
        var email = EmptyToNull(value);
        if (email is null) return null;
        if (email.Length > 256)
            throw new OperationCoreException("EMAIL_LENGTH", "Email is too long.", "E-posta çok uzun.", 400);
        return email;
    }

    private static void AssertStakeholderRevoke(string status, string? note)
    {
        if (string.Equals(status, PmStakeholderStatus.Revoked, StringComparison.Ordinal) && string.IsNullOrWhiteSpace(note))
        {
            throw new OperationCoreException(
                "STAKEHOLDER_NOTE",
                "A note is required to revoke a stakeholder.",
                "Geri almak için not zorunludur.",
                400);
        }
    }
}
