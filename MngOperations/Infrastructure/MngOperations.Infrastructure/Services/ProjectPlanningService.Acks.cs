using MngOperations.Application.Contracts.Planning;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Models;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed partial class ProjectPlanningService
{
    public async Task<ProjectAcknowledgementsDto> GetAcknowledgementsAsync(string projectId, CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(projectId, token, ct);
        var items = await LoadAcknowledgementDtosAsync(projectId, token, ct);
        return BuildAcknowledgements(items);
    }

    public async Task<AcknowledgementDto> CreateAcknowledgementAsync(
        string projectId,
        CreateAcknowledgementRequest request,
        CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(projectId, token, ct);
        var resourceId = RequireAckResourceId(request.ResourceId);
        var title = RequireAckTitle(request.Title);
        var personName = RequireAckPersonName(request.PersonName);
        var personId = EmptyToNull(request.PersonId);
        var versionLabel = NormalizeVersionLabel(request.VersionLabel);
        var wbsId = await NormalizeOptionalWbsIdAsync(projectId, request.WbsId, token, ct);
        var status = PmAckStatus.Normalize(request.Status);
        var note = EmptyToNull(request.Note);
        AssertAckNote(status, note);
        await AssertAckUniqueAsync(projectId, resourceId, versionLabel, personId, personName, excludeId: null, token, ct);

        var closed = PmAckStatus.IsClosed(status);
        var payload = new Dictionary<string, object?>
        {
            ["projectId"] = projectId,
            ["resourceId"] = resourceId,
            ["title"] = title,
            ["versionLabel"] = EmptyToNull(versionLabel),
            ["personName"] = personName,
            ["personId"] = personId,
            ["wbsId"] = wbsId,
            ["status"] = status,
            ["dueDate"] = request.DueDate,
            ["note"] = note,
            ["acknowledgedAt"] = closed ? DateTime.UtcNow : null,
            ["acknowledgedBy"] = closed ? EmptyToNull(_ctx.Username) : null
        };

        var created = await _dg.CreateAsync(PmDatasets.Acknowledgements, payload, token, ct);
        var id = ReadId(created);
        if (string.IsNullOrWhiteSpace(id))
            throw new OperationCoreException("CREATE_FAILED", "Acknowledgement create did not return an id.", "Okundu kaydı oluşturulamadı.", 500);
        return await LoadAcknowledgementDtoAsync(id, token, ct);
    }

    public async Task<AcknowledgementDto> UpdateAcknowledgementAsync(string id, UpdateAcknowledgementRequest request, CancellationToken ct = default)
    {
        var token = RequireToken();
        var existing = await LoadAcknowledgementRowOrThrowAsync(id, token, ct);
        var projectId = existing.projectId!;
        var resourceId = request.ResourceId is not null ? RequireAckResourceId(request.ResourceId) : RequireAckResourceId(existing.resourceId);
        var title = request.Title is not null ? RequireAckTitle(request.Title) : RequireAckTitle(existing.title);
        var personName = request.PersonName is not null ? RequireAckPersonName(request.PersonName) : RequireAckPersonName(existing.personName);
        var personId = request.PersonId is not null ? EmptyToNull(request.PersonId) : EmptyToNull(existing.personId);
        var versionLabel = request.VersionLabel is not null
            ? NormalizeVersionLabel(request.VersionLabel)
            : NormalizeVersionLabel(existing.versionLabel);
        var wbsId = request.WbsId is not null
            ? await NormalizeOptionalWbsIdAsync(projectId, request.WbsId, token, ct)
            : EmptyToNull(existing.wbsId);
        var status = PmAckStatus.Normalize(request.Status ?? existing.status);
        var note = request.Note is not null ? EmptyToNull(request.Note) : EmptyToNull(existing.note);
        AssertAckNote(status, note);
        await AssertAckUniqueAsync(projectId, resourceId, versionLabel, personId, personName, id, token, ct);

        var wasClosed = PmAckStatus.IsClosed(PmAckStatus.Normalize(existing.status));
        var nowClosed = PmAckStatus.IsClosed(status);

        var payload = new Dictionary<string, object?>();
        if (request.ResourceId is not null) payload["resourceId"] = resourceId;
        if (request.Title is not null) payload["title"] = title;
        if (request.VersionLabel is not null) payload["versionLabel"] = EmptyToNull(versionLabel);
        if (request.PersonName is not null) payload["personName"] = personName;
        if (request.PersonId is not null) payload["personId"] = personId;
        if (request.WbsId is not null) payload["wbsId"] = wbsId;
        if (request.Status is not null) payload["status"] = status;
        if (request.DueDate.HasValue) payload["dueDate"] = request.DueDate;
        if (request.Note is not null) payload["note"] = note;

        if (!nowClosed && wasClosed)
        {
            payload["status"] = status;
            payload["acknowledgedAt"] = null;
            payload["acknowledgedBy"] = null;
        }
        else if (nowClosed && !wasClosed)
        {
            payload["status"] = status;
            payload["acknowledgedAt"] = DateTime.UtcNow;
            payload["acknowledgedBy"] = EmptyToNull(_ctx.Username);
        }

        if (payload.Count > 0)
            await _dg.UpdateAsync(PmDatasets.Acknowledgements, id, payload, token, ct);
        return await LoadAcknowledgementDtoAsync(id, token, ct);
    }

    public async Task DeleteAcknowledgementAsync(string id, CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadAcknowledgementRowOrThrowAsync(id, token, ct);
        await _dg.DeleteAsync(PmDatasets.Acknowledgements, id, token, ct);
    }

    private async Task<List<AcknowledgementDto>> LoadAcknowledgementDtosAsync(string projectId, string token, CancellationToken ct)
    {
        var rows = await LoadAcknowledgementRowsAsync(projectId, token, ct);
        return rows
            .Select(ToAcknowledgementDto)
            .OrderByDescending(a => a.Pending)
            .ThenByDescending(a => a.Overdue)
            .ThenBy(a => a.DueDate)
            .ThenBy(a => a.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(a => a.PersonName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<List<PmAcknowledgementRow>> LoadAcknowledgementRowsAsync(string projectId, string token, CancellationToken ct)
    {
        var page = await _dg.QueryPageAsync(
            PmDatasets.Acknowledgements,
            new Dictionary<string, object?> { ["projectId"] = projectId },
            ListQuery,
            token,
            ct);
        return page.Items.Select(Map<PmAcknowledgementRow>).ToList();
    }

    private async Task<PmAcknowledgementRow> LoadAcknowledgementRowOrThrowAsync(string id, string token, CancellationToken ct)
    {
        var row = await _dg.GetByIdAsync<PmAcknowledgementRow>(PmDatasets.Acknowledgements, id, token, ct, expand: false);
        if (row is null || string.IsNullOrWhiteSpace(row.__dataId))
            throw new OperationCoreException("NOT_FOUND", "Acknowledgement not found.", "Okundu kaydı bulunamadı.", 404);
        return row;
    }

    private async Task<AcknowledgementDto> LoadAcknowledgementDtoAsync(string id, string token, CancellationToken ct)
    {
        var row = await LoadAcknowledgementRowOrThrowAsync(id, token, ct);
        return ToAcknowledgementDto(row);
    }

    private async Task AssertAckUniqueAsync(
        string projectId,
        string resourceId,
        string versionLabel,
        string? personId,
        string personName,
        string? excludeId,
        string token,
        CancellationToken ct)
    {
        var key = ResourceKey(personId, personName);
        var rows = await LoadAcknowledgementRowsAsync(projectId, token, ct);
        foreach (var row in rows)
        {
            if (!string.IsNullOrWhiteSpace(excludeId) && string.Equals(row.__dataId, excludeId, StringComparison.Ordinal))
                continue;
            if (!string.Equals((row.resourceId ?? string.Empty).Trim(), resourceId, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.Equals(NormalizeVersionLabel(row.versionLabel), versionLabel, StringComparison.OrdinalIgnoreCase))
                continue;
            var existingKey = ResourceKey(row.personId, row.personName);
            if (string.Equals(existingKey, key, StringComparison.Ordinal))
                throw new OperationCoreException(
                    "ACK_EXISTS",
                    "This person already has an acknowledgement for this document revision.",
                    "Bu kişi bu belge revizyonu için zaten kayıtlı.",
                    409);
        }
    }

    private static AcknowledgementDto ToAcknowledgementDto(PmAcknowledgementRow row)
    {
        var status = PmAckStatus.Normalize(row.status);
        var pending = PmAckStatus.IsPending(status);
        var due = row.dueDate?.ToUniversalTime().Date;
        var overdue = pending && due is not null && due.Value < DateTime.UtcNow.Date;
        return new AcknowledgementDto
        {
            Id = row.__dataId ?? string.Empty,
            ProjectId = row.projectId ?? string.Empty,
            ResourceId = (row.resourceId ?? string.Empty).Trim(),
            Title = (row.title ?? string.Empty).Trim(),
            VersionLabel = EmptyToNull(NormalizeVersionLabel(row.versionLabel)),
            PersonName = (row.personName ?? string.Empty).Trim(),
            PersonId = EmptyToNull(row.personId),
            WbsId = EmptyToNull(row.wbsId),
            Status = status,
            DueDate = row.dueDate,
            Note = EmptyToNull(row.note),
            AcknowledgedAt = row.acknowledgedAt,
            AcknowledgedBy = EmptyToNull(row.acknowledgedBy),
            Pending = pending,
            Overdue = overdue
        };
    }

    internal static ProjectAcknowledgementsDto BuildAcknowledgements(IReadOnlyList<AcknowledgementDto> items)
    {
        return new ProjectAcknowledgementsDto
        {
            PendingCount = items.Count(a => a.Pending),
            OverdueCount = items.Count(a => a.Overdue),
            Items = items
        };
    }

    private static string RequireAckResourceId(string? value)
    {
        var id = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(id))
            throw new OperationCoreException("RESOURCE_REQUIRED", "Document resource id is required.", "Belge kimliği zorunludur.", 400);
        if (id.Length > 64)
            throw new OperationCoreException("RESOURCE_LENGTH", "Document resource id is too long.", "Belge kimliği çok uzun.", 400);
        return id;
    }

    private static string RequireAckTitle(string? value)
    {
        var title = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title))
            throw new OperationCoreException("TITLE_REQUIRED", "Acknowledgement title is required.", "Okundu kaydı başlığı zorunludur.", 400);
        return title;
    }

    private static string RequireAckPersonName(string? value)
    {
        var name = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new OperationCoreException("NAME_REQUIRED", "Person name is required.", "Kişi adı zorunludur.", 400);
        return name;
    }

    private static string NormalizeVersionLabel(string? value) => (value ?? string.Empty).Trim();

    private static void AssertAckNote(string status, string? note)
    {
        if (string.Equals(status, PmAckStatus.Waived, StringComparison.Ordinal) && string.IsNullOrWhiteSpace(note))
        {
            throw new OperationCoreException(
                "ACK_NOTE",
                "A note is required to waive an acknowledgement.",
                "Feragat için not zorunludur.",
                400);
        }
    }
}
