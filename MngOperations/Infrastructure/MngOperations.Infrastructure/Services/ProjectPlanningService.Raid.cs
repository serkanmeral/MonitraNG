using MngOperations.Application.Contracts.Planning;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Models;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed partial class ProjectPlanningService
{
    public async Task<RaidItemDto> CreateRaidItemAsync(string projectId, CreateRaidItemRequest request, CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(projectId, token, ct);
        if (!PmRaidKind.TryNormalize(request.Kind, out var kind))
            throw new OperationCoreException("RAID_KIND", "Unknown RAID kind.", "Bilinmeyen RAID türü.", 400);

        var title = (request.Title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title))
            throw new OperationCoreException("TITLE_REQUIRED", "RAID title is required.", "RAID başlığı zorunludur.", 400);

        var status = PmRaidStatus.Normalize(kind, request.Status);
        var closed = !PmRaidStatus.IsOpen(kind, status);
        var wbsIds = await NormalizeWbsIdsAsync(projectId, request.WbsIds, token, ct);
        var payload = new Dictionary<string, object?>
        {
            ["projectId"] = projectId,
            ["kind"] = kind,
            ["title"] = title,
            ["body"] = EmptyToNull(request.Body),
            ["status"] = status,
            ["impact"] = PmRaidLevel.Normalize(request.Impact),
            ["likelihood"] = PmRaidLevel.Normalize(request.Likelihood),
            ["response"] = kind == PmRaidKind.Risk ? PmRaidResponse.Normalize(request.Response) : PmRaidResponse.None,
            ["owner"] = EmptyToNull(request.Owner),
            ["dueDate"] = request.DueDate,
            ["wbsIds"] = wbsIds,
            ["workItemIds"] = CleanIds(request.WorkItemIds),
            ["resourceIds"] = CleanIds(request.ResourceIds),
            ["closedAt"] = closed ? DateTime.UtcNow : null,
            ["closedBy"] = closed ? EmptyToNull(_ctx.Username) : null
        };

        var created = await _dg.CreateAsync(PmDatasets.RaidItems, payload, token, ct);
        var id = ReadId(created);
        if (string.IsNullOrWhiteSpace(id))
            throw new OperationCoreException("CREATE_FAILED", "RAID create did not return an id.", "RAID kaydı oluşturulamadı.", 500);
        return await LoadRaidItemDtoAsync(id, token, ct);
    }

    public async Task<RaidItemDto> UpdateRaidItemAsync(string id, UpdateRaidItemRequest request, CancellationToken ct = default)
    {
        var token = RequireToken();
        var existing = await LoadRaidItemRowOrThrowAsync(id, token, ct);
        var projectId = existing.projectId!;
        if (!PmRaidKind.TryNormalize(request.Kind ?? existing.kind, out var kind))
            throw new OperationCoreException("RAID_KIND", "Unknown RAID kind.", "Bilinmeyen RAID türü.", 400);

        var status = PmRaidStatus.Normalize(kind, request.Status ?? existing.status);
        var existingKind = PmRaidKind.TryNormalize(existing.kind, out var ek) ? ek : PmRaidKind.Risk;
        var existingStatus = PmRaidStatus.Normalize(existingKind, existing.status);
        var wasOpen = PmRaidStatus.IsOpen(existingKind, existingStatus);
        var nowOpen = PmRaidStatus.IsOpen(kind, status);

        var payload = new Dictionary<string, object?>();
        if (request.Kind is not null) payload["kind"] = kind;
        if (request.Title is not null)
        {
            var title = request.Title.Trim();
            if (string.IsNullOrWhiteSpace(title))
                throw new OperationCoreException("TITLE_REQUIRED", "RAID title is required.", "RAID başlığı zorunludur.", 400);
            payload["title"] = title;
        }
        if (request.Body is not null) payload["body"] = EmptyToNull(request.Body);
        if (request.Status is not null || request.Kind is not null) payload["status"] = status;
        if (request.Impact is not null) payload["impact"] = PmRaidLevel.Normalize(request.Impact);
        if (request.Likelihood is not null) payload["likelihood"] = PmRaidLevel.Normalize(request.Likelihood);
        if (request.Response is not null || request.Kind is not null)
            payload["response"] = kind == PmRaidKind.Risk ? PmRaidResponse.Normalize(request.Response ?? existing.response) : PmRaidResponse.None;
        if (request.Owner is not null) payload["owner"] = EmptyToNull(request.Owner);
        if (request.DueDate.HasValue) payload["dueDate"] = request.DueDate;
        if (request.WbsIds is not null) payload["wbsIds"] = await NormalizeWbsIdsAsync(projectId, request.WbsIds, token, ct);
        if (request.WorkItemIds is not null) payload["workItemIds"] = CleanIds(request.WorkItemIds);
        if (request.ResourceIds is not null) payload["resourceIds"] = CleanIds(request.ResourceIds);

        if (!nowOpen && wasOpen)
        {
            payload["closedAt"] = DateTime.UtcNow;
            payload["closedBy"] = EmptyToNull(_ctx.Username);
        }
        else if (nowOpen && !wasOpen)
        {
            payload["closedAt"] = null;
            payload["closedBy"] = null;
        }

        if (payload.Count > 0)
            await _dg.UpdateAsync(PmDatasets.RaidItems, id, payload, token, ct);
        return await LoadRaidItemDtoAsync(id, token, ct);
    }

    public async Task DeleteRaidItemAsync(string id, CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadRaidItemRowOrThrowAsync(id, token, ct);
        await _dg.DeleteAsync(PmDatasets.RaidItems, id, token, ct);
    }

    private async Task<List<RaidItemDto>> LoadRaidItemsAsync(string projectId, string token, CancellationToken ct)
    {
        var rows = await LoadRaidItemRowsAsync(projectId, token, ct);
        return rows
            .Select(ToRaidItemDto)
            .OrderByDescending(r => r.Open)
            .ThenByDescending(r => r.Score)
            .ThenBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<List<PmRaidItemRow>> LoadRaidItemRowsAsync(string projectId, string token, CancellationToken ct)
    {
        var page = await _dg.QueryPageAsync(
            PmDatasets.RaidItems,
            new Dictionary<string, object?> { ["projectId"] = projectId },
            ListQuery,
            token,
            ct);
        return page.Items.Select(Map<PmRaidItemRow>).ToList();
    }

    private async Task<PmRaidItemRow> LoadRaidItemRowOrThrowAsync(string id, string token, CancellationToken ct)
    {
        var row = await _dg.GetByIdAsync<PmRaidItemRow>(PmDatasets.RaidItems, id, token, ct, expand: false);
        if (row is null || string.IsNullOrWhiteSpace(row.__dataId))
            throw new OperationCoreException("NOT_FOUND", "RAID item not found.", "RAID kaydı bulunamadı.", 404);
        return row;
    }

    private async Task<RaidItemDto> LoadRaidItemDtoAsync(string id, string token, CancellationToken ct)
    {
        var row = await LoadRaidItemRowOrThrowAsync(id, token, ct);
        return ToRaidItemDto(row);
    }

    private static RaidItemDto ToRaidItemDto(PmRaidItemRow row)
    {
        var kind = PmRaidKind.TryNormalize(row.kind, out var k) ? k : PmRaidKind.Risk;
        var status = PmRaidStatus.Normalize(kind, row.status);
        var impact = PmRaidLevel.Normalize(row.impact);
        var likelihood = PmRaidLevel.Normalize(row.likelihood);
        var score = kind == PmRaidKind.Risk
            ? PmRaidLevel.Score(likelihood) * PmRaidLevel.Score(impact)
            : 0;
        var open = PmRaidStatus.IsOpen(kind, status);
        return new RaidItemDto
        {
            Id = row.__dataId ?? string.Empty,
            ProjectId = row.projectId ?? string.Empty,
            Kind = kind,
            Title = row.title ?? string.Empty,
            Body = row.body,
            Status = status,
            Impact = impact,
            Likelihood = likelihood,
            Response = kind == PmRaidKind.Risk ? PmRaidResponse.Normalize(row.response) : PmRaidResponse.None,
            Owner = row.owner,
            DueDate = row.dueDate,
            WbsIds = row.wbsIds ?? new List<string>(),
            WorkItemIds = row.workItemIds ?? new List<string>(),
            ResourceIds = row.resourceIds ?? new List<string>(),
            ClosedAt = row.closedAt,
            ClosedBy = row.closedBy,
            Score = score,
            Elevated = kind == PmRaidKind.Risk && (impact == PmRaidLevel.High || score >= 6),
            Open = open
        };
    }
}
