using Microsoft.Extensions.Logging;
using MngOperations.Application.Contracts.Planning;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Interfaces;
using MngOperations.Application.Models;
using MngOperations.Application.Utilities;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed partial class ProjectPlanningService
{
    private const int ProjectDocumentLimit = 80;
    private const int ProjectFolderWalkLimit = 40;

    public async Task<IReadOnlyList<ProjectDocumentDto>> ListProjectDocumentsAsync(
        string projectId,
        string? query,
        CancellationToken ct = default)
    {
        var token = RequireToken();
        var project = await LoadProjectOrThrowAsync(projectId, token, ct);
        if (string.IsNullOrWhiteSpace(project.diFolderId))
            return Array.Empty<ProjectDocumentDto>();

        var items = await LoadFolderDocumentsAsync(project.diFolderId, token, ct);
        var q = query?.Trim();
        if (!string.IsNullOrEmpty(q))
        {
            items = items
                .Where(d =>
                    d.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrWhiteSpace(d.Kind) && d.Kind.Contains(q, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        return items.Take(ProjectDocumentLimit).ToList();
    }

    public async Task<IReadOnlyList<TraceDocumentDto>> ListWbsEvidenceAsync(string wbsId, CancellationToken ct = default)
    {
        var token = RequireToken();
        var wbs = await LoadWbsOrThrowAsync(wbsId, token, ct);
        if (string.IsNullOrWhiteSpace(wbs.workItemId))
            return Array.Empty<TraceDocumentDto>();

        var docs = await LoadDocumentsByWorkItemAsync(new[] { wbs.workItemId }, token, ct);
        return (docs.GetValueOrDefault(wbs.workItemId) ?? [])
            .Where(d => IsEvidenceRelation(d.RelationType))
            .ToList();
    }

    public async Task<WbsItemDto> BindEvidenceAsync(string wbsId, BindWbsEvidenceRequest request, CancellationToken ct = default)
    {
        var token = RequireToken();
        var wbs = await LoadWbsOrThrowAsync(wbsId, token, ct);
        if (string.IsNullOrWhiteSpace(wbs.workItemId))
        {
            throw new OperationCoreException(
                "WI_UNBOUND",
                "Bind a work item before attaching evidence.",
                "Kanıt bağlamak için önce iş kaydı gerekir.",
                409);
        }

        var resourceId = (request.ResourceId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(resourceId))
            throw new OperationCoreException("DOC_REQUIRED", "Document id is required.", "Belge kimliği zorunludur.", 400);
        if (resourceId.Length > 64)
            throw new OperationCoreException("DOC_LENGTH", "Document id is too long.", "Belge kimliği çok uzun.", 400);

        var relationType = NormalizeEvidenceRelation(request.RelationType);
        var resource = await _dg.GetByIdAsync<Dictionary<string, object?>>(
            DmResources, resourceId, token, ct, expand: false);
        if (resource is null || string.IsNullOrWhiteSpace(WorkItemDataHelper.GetDataId(resource)))
            throw new OperationCoreException("NOT_FOUND", "Document not found.", "Belge bulunamadı.", 404);

        var type = WorkItemDataHelper.GetString(resource, "type");
        if (string.Equals(type, "folder", StringComparison.OrdinalIgnoreCase))
        {
            throw new OperationCoreException(
                "DOC_FOLDER",
                "A folder cannot be bound as evidence.",
                "Klasör kanıt olarak bağlanamaz.",
                400);
        }

        var existing = await FindWorkItemDocLinkAsync(wbs.workItemId, resourceId, relationType, token, ct);
        if (existing is not null)
        {
            throw new OperationCoreException(
                "EVIDENCE_BOUND",
                "This document is already bound as evidence.",
                "Bu belge zaten kanıt olarak bağlı.",
                409);
        }

        var payload = new Dictionary<string, object?>
        {
            ["resourceId"] = resourceId,
            ["targetModule"] = "operationCore",
            ["targetType"] = "workItem",
            ["targetId"] = wbs.workItemId,
            ["relationType"] = relationType,
            ["createdBy"] = EmptyToNull(_ctx.Username),
            ["createdAt"] = DateTime.UtcNow
        };

        var created = await _dg.CreateAsync(DmResourceLinks, payload, token, ct);
        if (string.IsNullOrWhiteSpace(ReadId(created)))
            throw new OperationCoreException("CREATE_FAILED", "Evidence link could not be created.", "Kanıt bağı oluşturulamadı.", 500);

        return await LoadHydratedWbsAsync(wbsId, wbs.projectId!, token, ct);
    }

    public async Task<WbsItemDto> UnbindEvidenceAsync(string wbsId, string resourceId, CancellationToken ct = default)
    {
        var token = RequireToken();
        var wbs = await LoadWbsOrThrowAsync(wbsId, token, ct);
        var rid = (resourceId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(rid))
            throw new OperationCoreException("DOC_REQUIRED", "Document id is required.", "Belge kimliği zorunludur.", 400);
        if (string.IsNullOrWhiteSpace(wbs.workItemId))
            throw new OperationCoreException("NOT_FOUND", "Evidence link not found.", "Kanıt bağı bulunamadı.", 404);

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
            if (!IsEvidenceRelation(relation))
                continue;
            var id = WorkItemDataHelper.GetDataId(row);
            if (string.IsNullOrWhiteSpace(id))
                continue;
            await _dg.DeleteAsync(DmResourceLinks, id, token, ct);
            removed++;
        }

        if (removed == 0)
            throw new OperationCoreException("NOT_FOUND", "Evidence link not found.", "Kanıt bağı bulunamadı.", 404);

        return await LoadHydratedWbsAsync(wbsId, wbs.projectId!, token, ct);
    }

    private async Task AssertDocumentsApprovedForCloseAsync(
        string workItemId,
        string wbsId,
        string token,
        CancellationToken ct)
    {
        var docs = await LoadDocumentsByWorkItemAsync(new[] { workItemId }, token, ct, throwOnError: true);
        var unapproved = (docs.GetValueOrDefault(workItemId) ?? []).FirstOrDefault(d => !d.Approved);
        if (unapproved is null)
            return;

        throw new OperationCoreException(
            "APPROVAL_REQUIRED",
            "Publish or approve linked documents before closing this work item.",
            "İşi kapatmadan önce bağlı belgeler yayınlanmış olmalıdır.",
            409,
            new Dictionary<string, object?>
            {
                ["wbsId"] = wbsId,
                ["workItemId"] = workItemId,
                ["resourceId"] = unapproved.ResourceId,
                ["documentName"] = unapproved.Name
            });
    }

    private async Task AssertEvidencePresentForCloseAsync(
        string workItemId,
        PmWbsRow wbs,
        string token,
        CancellationToken ct)
    {
        var wbsId = wbs.__dataId ?? string.Empty;
        // Summary / parent rows roll up from children; evidence is required on leaves only.
        if (string.Equals(PmWbsKind.Normalize(wbs.kind), PmWbsKind.Summary, StringComparison.Ordinal))
            return;
        if (!string.IsNullOrWhiteSpace(wbs.projectId) && !string.IsNullOrWhiteSpace(wbsId))
        {
            var siblings = await LoadWbsAsync(wbs.projectId, token, ct);
            if (siblings.Any(row => string.Equals(row.parentId, wbsId, StringComparison.Ordinal)))
                return;
        }

        var docs = await LoadDocumentsByWorkItemAsync(new[] { workItemId }, token, ct, throwOnError: true);
        if ((docs.GetValueOrDefault(workItemId) ?? []).Any(d => IsEvidenceRelation(d.RelationType)))
            return;

        throw new OperationCoreException(
            "EVIDENCE_REQUIRED",
            "Attach evidence before closing this work item.",
            "İşi kapatmadan önce kanıt belgesi bağlanmalıdır.",
            409,
            new Dictionary<string, object?>
            {
                ["wbsId"] = wbsId,
                ["workItemId"] = workItemId
            });
    }

    private async Task HydrateEvidenceAsync(IList<WbsItemDto> items, string token, CancellationToken ct) =>
        await HydrateDocumentLinksAsync(items, token, ct);

    private async Task<WbsItemDto> LoadHydratedWbsAsync(string wbsId, string projectId, string token, CancellationToken ct)
    {
        var row = await LoadWbsOrThrowAsync(wbsId, token, ct);
        var dto = ToWbsDto(row);
        var list = new List<WbsItemDto> { dto };
        await HydrateWorkItemsAsync(list, token, ct);
        await HydrateGateLocksAsync(projectId, list, token, ct);
        await HydrateDocumentLinksAsync(list, token, ct);
        return dto;
    }

    private static string NormalizeEvidenceRelation(string? value)
    {
        var relation = (value ?? "evidence").Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(relation) || relation == "evidence")
            return "evidence";
        if (relation == "output")
            return "output";
        throw new OperationCoreException(
            "RELATION_INVALID",
            "Evidence relation must be evidence or output.",
            "Kanıt ilişkisi evidence veya output olmalı.",
            400);
    }

    private async Task<List<ProjectDocumentDto>> LoadFolderDocumentsAsync(
        string folderId,
        string token,
        CancellationToken ct)
    {
        var result = new List<ProjectDocumentDto>();
        var pending = new Queue<string>();
        var seenFolders = new HashSet<string>(StringComparer.Ordinal) { folderId };
        pending.Enqueue(folderId);
        var walked = 0;

        while (pending.Count > 0 && walked < ProjectFolderWalkLimit && result.Count < ProjectDocumentLimit)
        {
            var current = pending.Dequeue();
            walked++;
            DataGatewayPage page;
            try
            {
                page = await _dg.QueryPageAsync(
                    DmResources,
                    new Dictionary<string, object?> { ["parentId"] = current },
                    "limit=200&expand=false",
                    token,
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "DI folder listing failed for {FolderId} (non-fatal)", current);
                continue;
            }

            foreach (var row in page.Items)
            {
                var id = WorkItemDataHelper.GetDataId(row);
                if (string.IsNullOrWhiteSpace(id))
                    continue;
                var type = WorkItemDataHelper.GetString(row, "type");
                if (string.Equals(type, "folder", StringComparison.OrdinalIgnoreCase))
                {
                    if (seenFolders.Add(id))
                        pending.Enqueue(id);
                    continue;
                }

                result.Add(new ProjectDocumentDto
                {
                    Id = id,
                    Name = WorkItemDataHelper.GetString(row, "title")
                           ?? WorkItemDataHelper.GetString(row, "name")
                           ?? id,
                    Kind = WorkItemDataHelper.GetString(row, "kind"),
                    Type = type,
                    Status = NormalizeDocStatus(WorkItemDataHelper.GetString(row, "status"))
                });

                if (result.Count >= ProjectDocumentLimit)
                    break;
            }
        }

        return result;
    }
}
