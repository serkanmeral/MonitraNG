using MngOperations.Application.Contracts.Planning;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Models;
using MngOperations.Application.Utilities;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed partial class ProjectPlanningService
{
    public async Task<DecisionDto> CreateDecisionAsync(string projectId, CreateDecisionRequest request, CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(projectId, token, ct);
        var title = (request.Title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title))
            throw new OperationCoreException("TITLE_REQUIRED", "Decision title is required.", "Karar başlığı zorunludur.", 400);

        var wbsIds = await NormalizeWbsIdsAsync(projectId, request.WbsIds, token, ct);
        var payload = new Dictionary<string, object?>
        {
            ["projectId"] = projectId,
            ["title"] = title,
            ["body"] = EmptyToNull(request.Body),
            ["kind"] = PmDecisionKind.Normalize(request.Kind),
            ["status"] = PmDecisionStatus.Normalize(request.Status),
            ["decidedAt"] = request.DecidedAt ?? DateTime.UtcNow,
            ["decidedBy"] = EmptyToNull(_ctx.Username),
            ["documentId"] = EmptyToNull(request.DocumentId),
            ["wbsIds"] = wbsIds,
            ["workItemIds"] = CleanIds(request.WorkItemIds),
            ["resourceIds"] = CleanIds(request.ResourceIds)
        };

        var created = await _dg.CreateAsync(PmDatasets.Decisions, payload, token, ct);
        var id = ReadId(created);
        if (string.IsNullOrWhiteSpace(id))
            throw new OperationCoreException("CREATE_FAILED", "Decision create did not return an id.", "Karar oluşturulamadı.", 500);
        return await LoadDecisionDtoAsync(id, token, ct);
    }

    public async Task<DecisionDto> UpdateDecisionAsync(string id, UpdateDecisionRequest request, CancellationToken ct = default)
    {
        var token = RequireToken();
        var existing = await LoadDecisionRowOrThrowAsync(id, token, ct);
        var projectId = existing.projectId!;
        var payload = new Dictionary<string, object?>();

        if (request.Title is not null)
        {
            var title = request.Title.Trim();
            if (string.IsNullOrWhiteSpace(title))
                throw new OperationCoreException("TITLE_REQUIRED", "Decision title is required.", "Karar başlığı zorunludur.", 400);
            payload["title"] = title;
        }
        if (request.Body is not null) payload["body"] = EmptyToNull(request.Body);
        if (request.Kind is not null) payload["kind"] = PmDecisionKind.Normalize(request.Kind);
        if (request.Status is not null) payload["status"] = PmDecisionStatus.Normalize(request.Status);
        if (request.DecidedAt.HasValue) payload["decidedAt"] = request.DecidedAt;
        if (request.DocumentId is not null) payload["documentId"] = EmptyToNull(request.DocumentId);
        if (request.WbsIds is not null) payload["wbsIds"] = await NormalizeWbsIdsAsync(projectId, request.WbsIds, token, ct);
        if (request.WorkItemIds is not null) payload["workItemIds"] = CleanIds(request.WorkItemIds);
        if (request.ResourceIds is not null) payload["resourceIds"] = CleanIds(request.ResourceIds);

        if (payload.Count > 0)
            await _dg.UpdateAsync(PmDatasets.Decisions, id, payload, token, ct);
        return await LoadDecisionDtoAsync(id, token, ct);
    }

    public async Task DeleteDecisionAsync(string id, CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadDecisionRowOrThrowAsync(id, token, ct);
        await _dg.DeleteAsync(PmDatasets.Decisions, id, token, ct);
    }

    private async Task<List<DecisionDto>> LoadDecisionsAsync(string projectId, string token, CancellationToken ct)
    {
        var rows = await LoadDecisionRowsAsync(projectId, token, ct);
        var dtos = rows.Select(ToDecisionDto).ToList();
        await HydrateDecisionDocumentsAsync(dtos, token, ct);
        return dtos
            .OrderByDescending(d => d.DecidedAt)
            .ThenBy(d => d.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<List<PmDecisionRow>> LoadDecisionRowsAsync(string projectId, string token, CancellationToken ct)
    {
        var page = await _dg.QueryPageAsync(
            PmDatasets.Decisions,
            new Dictionary<string, object?> { ["projectId"] = projectId },
            ListQuery,
            token,
            ct);
        return page.Items.Select(Map<PmDecisionRow>).ToList();
    }

    private async Task<PmDecisionRow> LoadDecisionRowOrThrowAsync(string id, string token, CancellationToken ct)
    {
        var row = await _dg.GetByIdAsync<PmDecisionRow>(PmDatasets.Decisions, id, token, ct, expand: false);
        if (row is null || string.IsNullOrWhiteSpace(row.__dataId))
            throw new OperationCoreException("NOT_FOUND", "Decision not found.", "Karar bulunamadı.", 404);
        return row;
    }

    private async Task<DecisionDto> LoadDecisionDtoAsync(string id, string token, CancellationToken ct)
    {
        var row = await LoadDecisionRowOrThrowAsync(id, token, ct);
        var dto = ToDecisionDto(row);
        await HydrateDecisionDocumentsAsync(new List<DecisionDto> { dto }, token, ct);
        return dto;
    }

    private async Task<List<string>> NormalizeWbsIdsAsync(
        string projectId,
        IReadOnlyList<string>? ids,
        string token,
        CancellationToken ct)
    {
        var clean = CleanIds(ids);
        if (clean.Count == 0) return clean;
        var wbs = await LoadWbsAsync(projectId, token, ct);
        var known = wbs.Select(w => w.__dataId).Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet(StringComparer.Ordinal);
        foreach (var id in clean)
        {
            if (!known.Contains(id))
                throw new OperationCoreException("WBS_PROJECT", "WBS item is not in the project.", "WBS kalemi bu projede değil.", 400);
        }
        return clean;
    }

    private static List<string> CleanIds(IReadOnlyList<string>? ids) =>
        (ids ?? Array.Empty<string>())
            .Select(id => id?.Trim())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static DecisionDto ToDecisionDto(PmDecisionRow row) => new()
    {
        Id = row.__dataId ?? string.Empty,
        ProjectId = row.projectId ?? string.Empty,
        Title = row.title ?? string.Empty,
        Body = row.body,
        Kind = PmDecisionKind.Normalize(row.kind),
        Status = PmDecisionStatus.Normalize(row.status),
        DecidedAt = row.decidedAt,
        DecidedBy = row.decidedBy,
        DocumentId = row.documentId,
        WbsIds = row.wbsIds ?? new List<string>(),
        WorkItemIds = row.workItemIds ?? new List<string>(),
        ResourceIds = row.resourceIds ?? new List<string>()
    };

    private async Task HydrateDecisionDocumentsAsync(IList<DecisionDto> items, string token, CancellationToken ct)
    {
        var ids = items
            .Select(i => i.DocumentId)
            .Concat(items.SelectMany(i => i.ResourceIds))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (ids.Count == 0) return;

        try
        {
            var page = await _dg.QueryPageAsync(
                DmResources,
                new Dictionary<string, object?>
                {
                    ["__dataId"] = new Dictionary<string, object?> { ["$in"] = ids.Cast<object?>().ToList() }
                },
                "limit=200&expand=false",
                token,
                ct);
            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var row in page.Items)
            {
                var id = WorkItemDataHelper.GetDataId(row);
                if (string.IsNullOrWhiteSpace(id)) continue;
                names[id] = WorkItemDataHelper.GetString(row, "title")
                    ?? WorkItemDataHelper.GetString(row, "name")
                    ?? id;
            }

            foreach (var item in items)
            {
                if (!string.IsNullOrWhiteSpace(item.DocumentId) && names.TryGetValue(item.DocumentId, out var name))
                    item.DocumentName = name;
            }
        }
        catch (Exception)
        {
            // Decision CRUD must not fail if DI catalog is unreachable.
        }
    }
}
