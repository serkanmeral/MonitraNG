using MngOperations.Application.Contracts.Planning;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Utilities;

namespace MngOperations.Infrastructure.Services;

public sealed partial class ProjectPlanningService
{
    public async Task<IReadOnlyList<TraceDocumentDto>> ListWbsReferencesAsync(string wbsId, CancellationToken ct = default)
    {
        var token = RequireToken();
        var wbs = await LoadWbsOrThrowAsync(wbsId, token, ct);
        if (string.IsNullOrWhiteSpace(wbs.workItemId))
            return Array.Empty<TraceDocumentDto>();

        var docs = await LoadDocumentsByWorkItemAsync(new[] { wbs.workItemId }, token, ct);
        return (docs.GetValueOrDefault(wbs.workItemId) ?? [])
            .Where(d => IsReferenceRelation(d.RelationType))
            .ToList();
    }

    public async Task<WbsItemDto> BindReferenceAsync(string wbsId, BindWbsReferenceRequest request, CancellationToken ct = default)
    {
        var token = RequireToken();
        var wbs = await LoadWbsOrThrowAsync(wbsId, token, ct);
        if (string.IsNullOrWhiteSpace(wbs.workItemId))
        {
            throw new OperationCoreException(
                "WI_UNBOUND",
                "Bind a work item before attaching a plan document.",
                "Plan belgesi bağlamak için önce iş kaydı gerekir.",
                409);
        }

        var resourceId = (request.ResourceId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(resourceId))
            throw new OperationCoreException("DOC_REQUIRED", "Document id is required.", "Belge kimliği zorunludur.", 400);
        if (resourceId.Length > 64)
            throw new OperationCoreException("DOC_LENGTH", "Document id is too long.", "Belge kimliği çok uzun.", 400);

        var resource = await _dg.GetByIdAsync<Dictionary<string, object?>>(
            DmResources, resourceId, token, ct, expand: false);
        if (resource is null || string.IsNullOrWhiteSpace(WorkItemDataHelper.GetDataId(resource)))
            throw new OperationCoreException("NOT_FOUND", "Document not found.", "Belge bulunamadı.", 404);

        var type = WorkItemDataHelper.GetString(resource, "type");
        if (string.Equals(type, "folder", StringComparison.OrdinalIgnoreCase))
        {
            throw new OperationCoreException(
                "DOC_FOLDER",
                "A folder cannot be bound as a plan document.",
                "Klasör plan belgesi olarak bağlanamaz.",
                400);
        }

        var existing = await FindWorkItemDocLinkAsync(wbs.workItemId, resourceId, "reference", token, ct);
        if (existing is not null)
        {
            throw new OperationCoreException(
                "REFERENCE_BOUND",
                "This document is already bound as a plan reference.",
                "Bu belge zaten plan/kaynak olarak bağlı.",
                409);
        }

        var payload = new Dictionary<string, object?>
        {
            ["resourceId"] = resourceId,
            ["targetModule"] = "operationCore",
            ["targetType"] = "workItem",
            ["targetId"] = wbs.workItemId,
            ["relationType"] = "reference",
            ["createdBy"] = EmptyToNull(_ctx.Username),
            ["createdAt"] = DateTime.UtcNow
        };

        var created = await _dg.CreateAsync(DmResourceLinks, payload, token, ct);
        if (string.IsNullOrWhiteSpace(ReadId(created)))
            throw new OperationCoreException("CREATE_FAILED", "Reference link could not be created.", "Plan bağı oluşturulamadı.", 500);

        return await LoadHydratedWbsAsync(wbsId, wbs.projectId!, token, ct);
    }

    public async Task<WbsItemDto> UnbindReferenceAsync(string wbsId, string resourceId, CancellationToken ct = default)
    {
        var token = RequireToken();
        var wbs = await LoadWbsOrThrowAsync(wbsId, token, ct);
        var rid = (resourceId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(rid))
            throw new OperationCoreException("DOC_REQUIRED", "Document id is required.", "Belge kimliği zorunludur.", 400);
        if (string.IsNullOrWhiteSpace(wbs.workItemId))
            throw new OperationCoreException("NOT_FOUND", "Reference link not found.", "Plan bağı bulunamadı.", 404);

        var links = await _dg.QueryPageAsync(
            DmResourceLinks,
            new Dictionary<string, object?>
            {
                ["resourceId"] = rid,
                ["targetModule"] = "operationCore",
                ["targetType"] = "workItem",
                ["targetId"] = wbs.workItemId
            },
            "limit=50&expand=false",
            token,
            ct);

        var removed = 0;
        foreach (var row in links.Items)
        {
            var relation = WorkItemDataHelper.GetString(row, "relationType");
            if (!IsReferenceRelation(relation))
                continue;
            var id = WorkItemDataHelper.GetDataId(row);
            if (string.IsNullOrWhiteSpace(id))
                continue;
            await _dg.DeleteAsync(DmResourceLinks, id, token, ct);
            removed++;
        }

        if (removed == 0)
            throw new OperationCoreException("NOT_FOUND", "Reference link not found.", "Plan bağı bulunamadı.", 404);

        return await LoadHydratedWbsAsync(wbsId, wbs.projectId!, token, ct);
    }

    private async Task HydrateDocumentLinksAsync(IList<WbsItemDto> items, string token, CancellationToken ct)
    {
        var workItemIds = items
            .Select(i => i.WorkItemId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (workItemIds.Count == 0)
            return;

        var docs = await LoadDocumentsByWorkItemAsync(workItemIds, token, ct);
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.WorkItemId))
                continue;
            var linked = docs.GetValueOrDefault(item.WorkItemId) ?? [];
            var evidence = linked.Count(d => IsEvidenceRelation(d.RelationType));
            item.EvidenceCount = evidence;
            item.HasEvidence = evidence > 0;
            var references = linked.Count(d => IsReferenceRelation(d.RelationType));
            item.ReferenceCount = references;
            item.HasReference = references > 0;
        }
    }

    private static bool IsReferenceRelation(string? relationType) =>
        string.Equals(relationType, "reference", StringComparison.OrdinalIgnoreCase);

    private async Task<Dictionary<string, object?>?> FindWorkItemDocLinkAsync(
        string workItemId,
        string resourceId,
        string relationType,
        string token,
        CancellationToken ct)
    {
        var page = await _dg.QueryPageAsync(
            DmResourceLinks,
            new Dictionary<string, object?>
            {
                ["resourceId"] = resourceId,
                ["targetModule"] = "operationCore",
                ["targetType"] = "workItem",
                ["targetId"] = workItemId,
                ["relationType"] = relationType
            },
            "limit=1&expand=false",
            token,
            ct);
        return page.Items.FirstOrDefault();
    }
}
