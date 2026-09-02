using System.Text.Json;
using MngDocument.Application.Contracts.ResourceLinks;
using MngDocument.Application.Contracts.Resources;
using MngDocument.Application.Exceptions;
using MngDocument.Application.Interfaces;
using MngDocument.Application.Models;
using MngDocument.Domain.Constants;

namespace MngDocument.Infrastructure.Services;

/// <summary>
/// <c>dm_resource_links</c> CRUD ve zenginleştirilmiş listeleme (Faz 2 — OperationCore work item).
/// </summary>
public sealed class ResourceLinkService : IResourceLinkService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string ListQuery = "limit=500&expand=false&showHistory=false";

    private readonly IMngDataGatewayClient _dg;
    private readonly IRequestContext _ctx;
    private readonly IPermissionService _perms;
    private readonly IRelationTypeCatalog _relationTypes;

    public ResourceLinkService(
        IMngDataGatewayClient dg,
        IRequestContext ctx,
        IPermissionService perms,
        IRelationTypeCatalog relationTypes)
    {
        _dg = dg;
        _ctx = ctx;
        _perms = perms;
        _relationTypes = relationTypes;
    }

    private string? Token => _ctx.BearerToken;

    public async Task<ResourceLinkDto> CreateAsync(CreateResourceLinkRequest request, CancellationToken ct = default)
    {
        var resourceId = request.ResourceId?.Trim() ?? string.Empty;
        var targetModule = request.TargetModule?.Trim() ?? string.Empty;
        var targetType = request.TargetType?.Trim() ?? string.Empty;
        var targetId = request.TargetId?.Trim() ?? string.Empty;
        var relationType = request.RelationType?.Trim().ToLowerInvariant() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(resourceId)
            || string.IsNullOrWhiteSpace(targetModule)
            || string.IsNullOrWhiteSpace(targetType)
            || string.IsNullOrWhiteSpace(targetId))
        {
            throw DocumentException.Validation(
                "LINK_VALIDATION",
                "resourceId, targetModule, targetType and targetId are required.",
                "Kaynak, hedef modül/tip/id zorunludur.");
        }

        if (!await _relationTypes.IsAllowedAsync(relationType, ct))
        {
            throw DocumentException.Validation(
                "LINK_RELATION_INVALID",
                "relationType is not allowed.",
                "Geçersiz ilişki tipi.");
        }

        var isWorkItem = ResourceLinkRelationType.IsWorkItemTarget(targetModule, targetType);
        var isResource = ResourceLinkRelationType.IsResourceTarget(targetModule, targetType);
        if (!isWorkItem && !isResource)
        {
            throw DocumentException.Validation(
                "LINK_TARGET_UNSUPPORTED",
                "Supported targets: operationCore/workItem or documentIntelligence/resource.",
                "Hedef operationCore/workItem veya documentIntelligence/resource olmalıdır.");
        }

        if (isResource && string.Equals(resourceId, targetId, StringComparison.OrdinalIgnoreCase))
        {
            throw DocumentException.Validation(
                "LINK_SELF",
                "A resource cannot link to itself.",
                "Kaynak kendisine bağlanamaz.");
        }

        var resource = await LoadResourceOrThrowAsync(resourceId, ct);
        var snapshot = await _perms.LoadSnapshotAsync(ct);
        snapshot.EnsureCan(resource, ResourceAction.View);

        if (isWorkItem)
            await EnsureWorkItemExistsAsync(targetId, ct);
        else
        {
            var targetResource = await LoadResourceOrThrowAsync(targetId, ct);
            snapshot.EnsureCan(targetResource, ResourceAction.View);
        }

        await EnsureLinkNotDuplicateAsync(resourceId, targetModule, targetType, targetId, relationType, ct);

        var payload = new Dictionary<string, object?>
        {
            ["resourceId"] = resourceId,
            ["targetModule"] = targetModule,
            ["targetType"] = targetType,
            ["targetId"] = targetId,
            ["relationType"] = relationType,
            ["createdBy"] = _ctx.Username,
            ["createdAt"] = DateTime.UtcNow
        };

        var created = await _dg.CreateAsync<DmResourceLink>(DmDatasets.ResourceLinks, payload, Token, ct);
        if (created.__dataId is null)
        {
            throw DocumentException.Validation(
                "LINK_CREATE_FAILED",
                "Link could not be created.",
                "Bağlantı oluşturulamadı.");
        }

        return ToDto(created);
    }

    public async Task DeleteAsync(string linkId, CancellationToken ct = default)
    {
        var id = linkId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(id))
            throw DocumentException.NotFound();

        var link = await _dg.GetByIdAsync<DmResourceLink>(DmDatasets.ResourceLinks, id, Token, ct);
        if (link is null || link.__dataId is null)
            throw DocumentException.NotFound();

        var resourceId = link.resourceId?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(resourceId))
        {
            var resource = await LoadResourceOrThrowAsync(resourceId, ct);
            var snapshot = await _perms.LoadSnapshotAsync(ct);
            snapshot.EnsureCan(resource, ResourceAction.View);
        }

        await _dg.DeleteAsync(DmDatasets.ResourceLinks, id, Token, ct);
    }

    public async Task<ResourceLinkListResult<LinkedWorkItemSummaryDto>> GetLinkedWorkItemsAsync(
        string resourceId,
        CancellationToken ct = default)
    {
        var rid = resourceId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rid))
            throw DocumentException.NotFound();

        var resource = await LoadResourceOrThrowAsync(rid, ct);
        var snapshot = await _perms.LoadSnapshotAsync(ct);
        snapshot.EnsureCan(resource, ResourceAction.View);

        var links = await QueryLinksByResourceAsync(rid, ct);
        var items = new List<LinkedWorkItemSummaryDto>();

        foreach (var link in links)
        {
            if (link.__dataId is null
                || !string.Equals(link.targetModule, ResourceLinkTargetModule.OperationCore, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(link.targetType, ResourceLinkTargetType.WorkItem, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var workItemId = link.targetId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(workItemId))
                continue;

            var workItem = await TryLoadWorkItemAsync(workItemId, ct);
            if (workItem is null)
                continue;

            items.Add(new LinkedWorkItemSummaryDto
            {
                LinkId = link.__dataId,
                WorkItemId = workItemId,
                WorkItemKey = GetString(workItem, "key") ?? GetString(workItem, "workItemKey"),
                WorkItemTitle = GetString(workItem, "title"),
                BoardId = GetString(workItem, "boardId"),
                WorkspaceId = GetString(workItem, "workspaceId"),
                RelationType = link.relationType ?? ResourceLinkRelationType.Reference
            });
        }

        return new ResourceLinkListResult<LinkedWorkItemSummaryDto>
        {
            Items = items,
            Total = items.Count
        };
    }

    public async Task<ResourceLinkListResult<LinkedResourceSummaryDto>> GetLinkedResourcesForWorkItemAsync(
        string workItemId,
        CancellationToken ct = default)
    {
        var wid = workItemId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(wid))
        {
            throw DocumentException.Validation(
                "WORK_ITEM_ID_REQUIRED",
                "workItemId is required.",
                "İş kaydı id zorunludur.");
        }

        await EnsureWorkItemExistsAsync(wid, ct);

        var match = new Dictionary<string, object?>
        {
            ["targetModule"] = ResourceLinkTargetModule.OperationCore,
            ["targetType"] = ResourceLinkTargetType.WorkItem,
            ["targetId"] = wid
        };

        var page = await _dg.QueryPageAsync(DmDatasets.ResourceLinks, match, ListQuery, Token, ct);
        var links = page.Items.Select(MapLinkRow).Where(l => l.__dataId is not null).ToList();

        var snapshot = await _perms.LoadSnapshotAsync(ct);
        var items = new List<LinkedResourceSummaryDto>();

        foreach (var link in links)
        {
            var resourceId = link.resourceId?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(resourceId))
                continue;

            var resource = await TryLoadResourceAsync(resourceId, ct);
            if (resource is null)
                continue;

            var eff = snapshot.Resolve(resource);
            if (!eff.CanView)
                continue;

            items.Add(new LinkedResourceSummaryDto
            {
                LinkId = link.__dataId!,
                ResourceId = resourceId,
                RelationType = link.relationType ?? ResourceLinkRelationType.Reference,
                ResourceType = resource.type,
                Name = resource.name,
                Title = resource.title,
                MimeType = resource.mimeType,
                Extension = resource.extension,
                Permissions = eff
            });
        }

        return new ResourceLinkListResult<LinkedResourceSummaryDto>
        {
            Items = items,
            Total = items.Count
        };
    }

    public async Task<ResourceLinkListResult<LinkedResourceSummaryDto>> GetRelatedResourcesAsync(
        string resourceId,
        CancellationToken ct = default)
    {
        var rid = resourceId?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rid))
            throw DocumentException.NotFound();

        var source = await LoadResourceOrThrowAsync(rid, ct);
        var snapshot = await _perms.LoadSnapshotAsync(ct);
        snapshot.EnsureCan(source, ResourceAction.View);

        var outgoing = await QueryLinksByResourceAsync(rid, ct);
        var incomingPage = await _dg.QueryPageAsync(
            DmDatasets.ResourceLinks,
            new Dictionary<string, object?>
            {
                ["targetModule"] = ResourceLinkTargetModule.DocumentIntelligence,
                ["targetType"] = ResourceLinkTargetType.Resource,
                ["targetId"] = rid
            },
            ListQuery,
            Token,
            ct);

        var items = new List<LinkedResourceSummaryDto>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var link in outgoing)
        {
            if (link.__dataId is null || !ResourceLinkRelationType.IsResourceTarget(link.targetModule, link.targetType))
                continue;
            var otherId = link.targetId?.Trim() ?? string.Empty;
            var dto = await TryMapRelatedAsync(link.__dataId, otherId, link.relationType, "outgoing", snapshot, ct);
            if (dto is null || !seen.Add(dto.LinkId))
                continue;
            items.Add(dto);
        }

        foreach (var row in incomingPage.Items)
        {
            var link = MapLinkRow(row);
            if (link.__dataId is null)
                continue;
            var otherId = link.resourceId?.Trim() ?? string.Empty;
            var dto = await TryMapRelatedAsync(link.__dataId, otherId, link.relationType, "incoming", snapshot, ct);
            if (dto is null || !seen.Add(dto.LinkId))
                continue;
            items.Add(dto);
        }

        return new ResourceLinkListResult<LinkedResourceSummaryDto>
        {
            Items = items,
            Total = items.Count
        };
    }

    private async Task<LinkedResourceSummaryDto?> TryMapRelatedAsync(
        string linkId,
        string otherId,
        string? relationType,
        string direction,
        PermissionSnapshot snapshot,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(otherId))
            return null;
        var other = await _dg.GetByIdAsync<DmResource>(DmDatasets.Resources, otherId, Token, ct);
        if (other?.__dataId is null)
            return null;
        if (!PermissionSnapshot.Allows(snapshot.Resolve(other), ResourceAction.View))
            return null;

        return new LinkedResourceSummaryDto
        {
            LinkId = linkId,
            ResourceId = other.__dataId,
            RelationType = relationType ?? ResourceLinkRelationType.Reference,
            Direction = direction,
            Kind = other.kind,
            ResourceType = other.type,
            Name = other.name,
            Title = other.title,
            MimeType = other.mimeType,
            Extension = other.extension,
            Permissions = snapshot.Resolve(other)
        };
    }

    private async Task EnsureWorkItemExistsAsync(string workItemId, CancellationToken ct)
    {
        var workItem = await TryLoadWorkItemAsync(workItemId, ct);
        if (workItem is null)
        {
            throw DocumentException.NotFound("İş kaydı bulunamadı.");
        }
    }

    private async Task EnsureLinkNotDuplicateAsync(
        string resourceId,
        string targetModule,
        string targetType,
        string targetId,
        string relationType,
        CancellationToken ct)
    {
        var match = new Dictionary<string, object?>
        {
            ["resourceId"] = resourceId,
            ["targetModule"] = targetModule,
            ["targetType"] = targetType,
            ["targetId"] = targetId,
            ["relationType"] = relationType
        };

        var page = await _dg.QueryPageAsync(DmDatasets.ResourceLinks, match, "limit=1&expand=false", Token, ct);
        if (page.Items.Count > 0)
        {
            throw DocumentException.Conflict(
                "LINK_ALREADY_EXISTS",
                "This link already exists.",
                "Bu bağlantı zaten mevcut.");
        }
    }

    private async Task<List<DmResourceLink>> QueryLinksByResourceAsync(string resourceId, CancellationToken ct)
    {
        var match = new Dictionary<string, object?> { ["resourceId"] = resourceId };
        var page = await _dg.QueryPageAsync(DmDatasets.ResourceLinks, match, ListQuery, Token, ct);
        return page.Items.Select(MapLinkRow).ToList();
    }

    private async Task<DmResource> LoadResourceOrThrowAsync(string id, CancellationToken ct)
    {
        var resource = await TryLoadResourceAsync(id, ct);
        if (resource is null || resource.__dataId is null)
            throw DocumentException.NotFound();
        return resource;
    }

    private async Task<DmResource?> TryLoadResourceAsync(string id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        return await _dg.GetByIdAsync<DmResource>(DmDatasets.Resources, id, Token, ct);
    }

    private async Task<Dictionary<string, object?>?> TryLoadWorkItemAsync(string id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        try
        {
            var row = await _dg.GetByIdAsync<Dictionary<string, object?>>(OcDatasets.WorkItems, id, Token, ct);
            return row;
        }
        catch
        {
            return null;
        }
    }

    private static DmResourceLink MapLinkRow(Dictionary<string, object?> row)
    {
        var json = JsonSerializer.Serialize(row, JsonOptions);
        return JsonSerializer.Deserialize<DmResourceLink>(json, JsonOptions) ?? new DmResourceLink();
    }

    private static ResourceLinkDto ToDto(DmResourceLink link) =>
        new()
        {
            Id = link.__dataId ?? string.Empty,
            ResourceId = link.resourceId ?? string.Empty,
            TargetModule = link.targetModule ?? string.Empty,
            TargetType = link.targetType ?? string.Empty,
            TargetId = link.targetId ?? string.Empty,
            RelationType = link.relationType ?? ResourceLinkRelationType.Reference,
            CreatedBy = link.createdBy,
            CreatedAt = link.createdAt
        };

    private static string? GetString(IReadOnlyDictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value) || value is null)
            return null;
        var s = value.ToString();
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }
}
