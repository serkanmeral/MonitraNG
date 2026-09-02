using MngOperations.Application.Contracts.Planning;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Models;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed partial class ProjectPlanningService
{
    public async Task<ProjectProcessMapsDto> GetProcessMapsAsync(string projectId, CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(projectId, token, ct);
        var items = await LoadProcessMapDtosAsync(projectId, token, ct);
        return BuildProcessMaps(items);
    }

    public async Task<ProcessMapDto> CreateProcessMapAsync(
        string projectId,
        CreateProcessMapRequest request,
        CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(projectId, token, ct);
        var name = RequireProcessMapName(request.Name);
        var kind = PmProcessMapKind.Normalize(request.Kind);
        var wbsId = await NormalizeOptionalWbsIdAsync(projectId, request.WbsId, token, ct);
        var status = PmProcessMapStatus.Normalize(request.Status);
        var resourceId = NormalizeOptionalId(
            request.ResourceId,
            "RESOURCE_LENGTH",
            "Process map document id is too long.",
            "Süreç haritası belge kimliği çok uzun.");
        var note = EmptyToNull(request.Note);
        AssertProcessMapState(status, resourceId, note);
        await AssertProcessMapUniqueAsync(projectId, name, excludeId: null, token, ct);

        var payload = new Dictionary<string, object?>
        {
            ["projectId"] = projectId,
            ["name"] = name,
            ["kind"] = kind,
            ["resourceId"] = resourceId,
            ["wbsId"] = wbsId,
            ["status"] = status,
            ["note"] = note,
            ["currentAt"] = PmProcessMapStatus.IsCurrent(status) ? DateTime.UtcNow : null,
            ["currentBy"] = PmProcessMapStatus.IsCurrent(status) ? EmptyToNull(_ctx.Username) : null,
            ["supersededAt"] = PmProcessMapStatus.IsClosed(status) ? DateTime.UtcNow : null,
            ["supersededBy"] = PmProcessMapStatus.IsClosed(status) ? EmptyToNull(_ctx.Username) : null
        };

        var created = await _dg.CreateAsync(PmDatasets.ProcessMaps, payload, token, ct);
        var id = ReadId(created);
        if (string.IsNullOrWhiteSpace(id))
            throw new OperationCoreException("CREATE_FAILED", "Process map create did not return an id.", "Süreç haritası oluşturulamadı.", 500);
        return await LoadProcessMapDtoAsync(id, token, ct);
    }

    public async Task<ProcessMapDto> UpdateProcessMapAsync(string id, UpdateProcessMapRequest request, CancellationToken ct = default)
    {
        var token = RequireToken();
        var existing = await LoadProcessMapRowOrThrowAsync(id, token, ct);
        var projectId = existing.projectId!;
        var name = request.Name is not null ? RequireProcessMapName(request.Name) : RequireProcessMapName(existing.name);
        var kind = PmProcessMapKind.Normalize(request.Kind ?? existing.kind);
        var wbsId = request.WbsId is not null
            ? await NormalizeOptionalWbsIdAsync(projectId, request.WbsId, token, ct)
            : EmptyToNull(existing.wbsId);
        var status = PmProcessMapStatus.Normalize(request.Status ?? existing.status);
        var resourceId = request.ResourceId is not null
            ? NormalizeOptionalId(
                request.ResourceId,
                "RESOURCE_LENGTH",
                "Process map document id is too long.",
                "Süreç haritası belge kimliği çok uzun.")
            : EmptyToNull(existing.resourceId);
        var note = request.Note is not null ? EmptyToNull(request.Note) : EmptyToNull(existing.note);
        AssertProcessMapState(status, resourceId, note);
        await AssertProcessMapUniqueAsync(projectId, name, id, token, ct);

        var wasCurrent = PmProcessMapStatus.IsCurrent(PmProcessMapStatus.Normalize(existing.status));
        var nowCurrent = PmProcessMapStatus.IsCurrent(status);
        var wasClosed = PmProcessMapStatus.IsClosed(PmProcessMapStatus.Normalize(existing.status));
        var nowClosed = PmProcessMapStatus.IsClosed(status);

        var payload = new Dictionary<string, object?>();
        if (request.Name is not null) payload["name"] = name;
        if (request.Kind is not null) payload["kind"] = kind;
        if (request.ResourceId is not null) payload["resourceId"] = resourceId;
        if (request.WbsId is not null) payload["wbsId"] = wbsId;
        if (request.Status is not null) payload["status"] = status;
        if (request.Note is not null) payload["note"] = note;

        if (nowCurrent && !wasCurrent)
        {
            payload["status"] = status;
            payload["currentAt"] = DateTime.UtcNow;
            payload["currentBy"] = EmptyToNull(_ctx.Username);
            payload["supersededAt"] = null;
            payload["supersededBy"] = null;
        }
        else if (nowClosed && !wasClosed)
        {
            payload["status"] = status;
            payload["supersededAt"] = DateTime.UtcNow;
            payload["supersededBy"] = EmptyToNull(_ctx.Username);
        }
        else if (!nowCurrent && wasCurrent && !nowClosed)
        {
            payload["status"] = status;
            payload["currentAt"] = null;
            payload["currentBy"] = null;
        }
        else if (!nowClosed && wasClosed)
        {
            payload["status"] = status;
            payload["supersededAt"] = null;
            payload["supersededBy"] = null;
        }

        if (payload.Count > 0)
            await _dg.UpdateAsync(PmDatasets.ProcessMaps, id, payload, token, ct);
        return await LoadProcessMapDtoAsync(id, token, ct);
    }

    public async Task DeleteProcessMapAsync(string id, CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProcessMapRowOrThrowAsync(id, token, ct);
        await _dg.DeleteAsync(PmDatasets.ProcessMaps, id, token, ct);
    }

    private async Task<List<ProcessMapDto>> LoadProcessMapDtosAsync(string projectId, string token, CancellationToken ct)
    {
        var rows = await LoadProcessMapRowsAsync(projectId, token, ct);
        return rows
            .Select(ToProcessMapDto)
            .OrderByDescending(p => p.Current)
            .ThenByDescending(p => p.Open)
            .ThenByDescending(p => p.Incomplete)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<List<PmProcessMapRow>> LoadProcessMapRowsAsync(string projectId, string token, CancellationToken ct)
    {
        var page = await _dg.QueryPageAsync(
            PmDatasets.ProcessMaps,
            new Dictionary<string, object?> { ["projectId"] = projectId },
            ListQuery,
            token,
            ct);
        return page.Items.Select(Map<PmProcessMapRow>).ToList();
    }

    private async Task<PmProcessMapRow> LoadProcessMapRowOrThrowAsync(string id, string token, CancellationToken ct)
    {
        var row = await _dg.GetByIdAsync<PmProcessMapRow>(PmDatasets.ProcessMaps, id, token, ct, expand: false);
        if (row is null || string.IsNullOrWhiteSpace(row.__dataId))
            throw new OperationCoreException("NOT_FOUND", "Process map not found.", "Süreç haritası bulunamadı.", 404);
        return row;
    }

    private async Task<ProcessMapDto> LoadProcessMapDtoAsync(string id, string token, CancellationToken ct)
    {
        var row = await LoadProcessMapRowOrThrowAsync(id, token, ct);
        return ToProcessMapDto(row);
    }

    private async Task AssertProcessMapUniqueAsync(
        string projectId,
        string name,
        string? excludeId,
        string token,
        CancellationToken ct)
    {
        var rows = await LoadProcessMapRowsAsync(projectId, token, ct);
        foreach (var row in rows)
        {
            if (!string.IsNullOrWhiteSpace(excludeId) && string.Equals(row.__dataId, excludeId, StringComparison.Ordinal))
                continue;
            if (string.Equals((row.name ?? string.Empty).Trim(), name, StringComparison.OrdinalIgnoreCase))
                throw new OperationCoreException(
                    "PROCESS_MAP_EXISTS",
                    "This process map already exists on the project.",
                    "Bu süreç haritası bu projede zaten var.",
                    409);
        }
    }

    private static ProcessMapDto ToProcessMapDto(PmProcessMapRow row)
    {
        var status = PmProcessMapStatus.Normalize(row.status);
        var open = PmProcessMapStatus.IsOpen(status);
        var resourceId = EmptyToNull(row.resourceId);
        return new ProcessMapDto
        {
            Id = row.__dataId ?? string.Empty,
            ProjectId = row.projectId ?? string.Empty,
            Name = (row.name ?? string.Empty).Trim(),
            Kind = PmProcessMapKind.Normalize(row.kind),
            ResourceId = resourceId,
            WbsId = EmptyToNull(row.wbsId),
            Status = status,
            Note = EmptyToNull(row.note),
            CurrentAt = row.currentAt,
            CurrentBy = EmptyToNull(row.currentBy),
            SupersededAt = row.supersededAt,
            SupersededBy = EmptyToNull(row.supersededBy),
            Open = open,
            Incomplete = open && resourceId is null,
            Current = PmProcessMapStatus.IsCurrent(status)
        };
    }

    internal static ProjectProcessMapsDto BuildProcessMaps(IReadOnlyList<ProcessMapDto> items)
    {
        return new ProjectProcessMapsDto
        {
            OpenCount = items.Count(p => p.Open),
            IncompleteCount = items.Count(p => p.Incomplete),
            CurrentCount = items.Count(p => p.Current),
            Items = items
        };
    }

    private static string RequireProcessMapName(string? value)
    {
        var name = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new OperationCoreException("NAME_REQUIRED", "Process map name is required.", "Süreç haritası adı zorunludur.", 400);
        return name;
    }

    private static void AssertProcessMapState(string status, string? resourceId, string? note)
    {
        if (PmProcessMapStatus.IsCurrent(status) && string.IsNullOrWhiteSpace(resourceId))
        {
            throw new OperationCoreException(
                "PROCESS_MAP_RESOURCE",
                "A document is required to mark a process map current.",
                "Resmi süreç için belge zorunludur.",
                400);
        }

        if (PmProcessMapStatus.IsClosed(status) && string.IsNullOrWhiteSpace(note))
        {
            throw new OperationCoreException(
                "PROCESS_MAP_NOTE",
                "A note is required to supersede a process map.",
                "Yürürlükten kaldırmak için not zorunludur.",
                400);
        }
    }
}
