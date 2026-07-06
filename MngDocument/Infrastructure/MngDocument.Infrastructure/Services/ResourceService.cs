using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngDocument.Application.Configuration;
using MngDocument.Application.Contracts.Resources;
using MngDocument.Application.Exceptions;
using MngDocument.Application.Interfaces;
using MngDocument.Application.Models;
using MngDocument.Application.Utilities;
using MngDocument.Domain.Constants;

namespace MngDocument.Infrastructure.Services;

/// <summary>
/// Faz 1 kaynak orkestrasyonu. Kalıcılık DG <c>dm_*</c> dataset'leri üzerinden; Mongo'ya
/// doğrudan dokunulmaz. Faz 1'de yetki minimum (domain içi açık) — DG token ile izole edilir.
/// </summary>
public class ResourceService : IResourceService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string ListQuery = "limit=1000&expand=false&showHistory=true";

    private readonly IMngDataGatewayClient _dg;
    private readonly IRequestContext _ctx;
    private readonly IPermissionService _perms;
    private readonly MngDocumentSettings _settings;
    private readonly ILogger<ResourceService> _logger;

    public ResourceService(
        IMngDataGatewayClient dg,
        IRequestContext ctx,
        IPermissionService perms,
        IOptions<MngDocumentSettings> settings,
        ILogger<ResourceService> logger)
    {
        _dg = dg;
        _ctx = ctx;
        _perms = perms;
        _settings = settings.Value;
        _logger = logger;
    }

    private string? Token => _ctx.BearerToken;

    public async Task<IReadOnlyList<TreeNodeDto>> GetTreeAsync(CancellationToken ct = default)
    {
        var snapshot = await _perms.LoadSnapshotAsync(ct);
        return BuildTreeFromSnapshot(snapshot);
    }

    public async Task<ResourceBootstrapDto> GetBootstrapAsync(string? folderId = null, CancellationToken ct = default)
    {
        var snapshot = await _perms.LoadSnapshotAsync(ct);
        var tree = BuildTreeFromSnapshot(snapshot);
        var children = await QueryChildrenAsync(NormalizeParentId(folderId), snapshot, ct);
        var (breadcrumb, selectedFolder) = await ResolveFolderContextAsync(folderId, snapshot, ct);

        return new ResourceBootstrapDto
        {
            Tree = tree,
            Children = children,
            Breadcrumb = breadcrumb,
            SelectedFolder = selectedFolder
        };
    }

    public async Task<ResourceBrowseContextDto> GetBrowseContextAsync(string? folderId, CancellationToken ct = default)
    {
        var snapshot = await _perms.LoadSnapshotAsync(ct);
        var children = await QueryChildrenAsync(NormalizeParentId(folderId), snapshot, ct);
        var (breadcrumb, selectedFolder) = await ResolveFolderContextAsync(folderId, snapshot, ct);

        return new ResourceBrowseContextDto
        {
            Children = children,
            Breadcrumb = breadcrumb,
            SelectedFolder = selectedFolder
        };
    }

    public async Task<ResourceListResult> GetChildrenAsync(string? parentId, CancellationToken ct = default)
    {
        var snapshot = await _perms.LoadSnapshotAsync(ct);
        return await QueryChildrenAsync(NormalizeParentId(parentId), snapshot, ct);
    }

    public async Task<ResourceDto> GetByIdAsync(string id, CancellationToken ct = default)
    {
        var resource = await LoadOrThrowAsync(id, ct);
        var snapshot = await _perms.LoadSnapshotAsync(ct);
        snapshot.EnsureCan(resource, ResourceAction.View);
        return ToDto(resource, snapshot.Resolve(resource));
    }

    public async Task<IReadOnlyList<BreadcrumbDto>> GetBreadcrumbAsync(string id, CancellationToken ct = default)
    {
        var resource = await LoadOrThrowAsync(id, ct);
        var snapshot = await _perms.LoadSnapshotAsync(ct);
        snapshot.EnsureCan(resource, ResourceAction.View);
        return await BuildBreadcrumbAsync(resource, ct);
    }

    public async Task<ResourceDto> CreateFolderAsync(CreateFolderRequest request, CancellationToken ct = default)
    {
        ValidateName(request.Name);
        await EnsureCanOnParentAsync(request.ParentId, ResourceAction.Create, ct);
        var ancestorIds = await ResolveAncestorsForChildAsync(request.ParentId, ct);

        var payload = new Dictionary<string, object?>
        {
            ["type"] = ResourceType.Folder,
            ["parentId"] = request.ParentId,
            ["ancestorIds"] = ancestorIds,
            ["name"] = request.Name.Trim(),
            ["title"] = request.Name.Trim(),
            ["description"] = request.Description,
            ["tags"] = request.Tags ?? new List<string>(),
            ["currentVersionNumber"] = 1
        };

        var created = await _dg.CreateAsync<DmResource>(DmDatasets.Resources, payload, Token, ct);
        _perms.InvalidateSnapshotCache();
        return await ToDtoWithEffectiveAsync(created, ct);
    }

    public async Task<ResourceDto> RenameAsync(string id, RenameResourceRequest request, CancellationToken ct = default)
    {
        ValidateName(request.Name);
        var resource = await LoadOrThrowAsync(id, ct);
        var snapshot = await _perms.LoadSnapshotAsync(ct);
        snapshot.EnsureCan(resource, ResourceAction.Edit);

        var payload = new Dictionary<string, object?>
        {
            ["name"] = request.Name.Trim(),
            ["title"] = request.Name.Trim()
        };

        var updated = await _dg.UpdateAsync<DmResource>(DmDatasets.Resources, id, payload, Token, ct);
        if (resource.type == ResourceType.Folder)
            _perms.InvalidateSnapshotCache();
        return ToDto(updated, snapshot.Resolve(updated));
    }

    public async Task<ResourceDto> MoveAsync(string id, MoveResourceRequest request, CancellationToken ct = default)
    {
        var node = await LoadOrThrowAsync(id, ct);
        var newParentId = string.IsNullOrWhiteSpace(request.NewParentId) ? null : request.NewParentId;

        if (newParentId == id)
            throw DocumentException.Validation("INVALID_MOVE", "Cannot move a resource into itself.", "Bir kaynak kendi içine taşınamaz.");

        var snapshot = await _perms.LoadSnapshotAsync(ct);
        snapshot.EnsureCan(node, ResourceAction.Move);

        List<string> newAncestors;
        if (newParentId is null)
        {
            newAncestors = new List<string>();
        }
        else
        {
            var newParent = await LoadOrThrowAsync(newParentId, ct);
            if (newParent.type != ResourceType.Folder)
                throw DocumentException.Validation("INVALID_PARENT", "Target parent must be a folder.", "Hedef yalnızca klasör olabilir.");

            if ((newParent.ancestorIds ?? new List<string>()).Contains(id))
                throw DocumentException.Validation("INVALID_MOVE", "Cannot move a folder into its own descendant.", "Bir klasör kendi alt klasörüne taşınamaz.");

            snapshot.EnsureCan(newParent, ResourceAction.Create);

            newAncestors = new List<string>(newParent.ancestorIds ?? new List<string>()) { newParentId };
        }

        var payload = new Dictionary<string, object?>
        {
            ["parentId"] = newParentId,
            ["ancestorIds"] = newAncestors
        };
        var updated = await _dg.UpdateAsync<DmResource>(DmDatasets.Resources, id, payload, Token, ct);

        await ReindexDescendantsAsync(id, newAncestors, ct);

        _perms.InvalidateSnapshotCache();
        var refreshed = await _perms.LoadSnapshotAsync(ct);
        return ToDto(updated, refreshed.Resolve(updated));
    }

    public async Task DeleteAsync(string id, bool force, CancellationToken ct = default)
    {
        var node = await LoadOrThrowAsync(id, ct);
        var snapshot = await _perms.LoadSnapshotAsync(ct);
        snapshot.EnsureCan(node, ResourceAction.Delete);

        if (node.type == ResourceType.Folder)
        {
            var descendants = await GetDescendantsAsync(id, ct);
            if (descendants.Count > 0 && !force)
            {
                throw DocumentException.Conflict(
                    "FOLDER_NOT_EMPTY",
                    "Folder is not empty. Use force to delete recursively.",
                    "Klasör boş değil. Özyinelemeli silmek için force kullanın.");
            }

            foreach (var descendant in descendants)
            {
                if (descendant.__dataId is not null)
                    await DeleteResourceAndVersionsAsync(descendant.__dataId, descendant.type, ct);
            }
        }

        await DeleteResourceAndVersionsAsync(id, node.type, ct);
        _perms.InvalidateSnapshotCache();
    }

    public async Task<ResourceDto> CreateMarkdownAsync(CreateMarkdownRequest request, CancellationToken ct = default)
    {
        ValidateName(request.Title);
        ValidateContentLength(request.Content);
        await EnsureCanOnParentAsync(request.ParentId, ResourceAction.Create, ct);
        var ancestorIds = await ResolveAncestorsForChildAsync(request.ParentId, ct);

        var payload = new Dictionary<string, object?>
        {
            ["type"] = ResourceType.Markdown,
            ["parentId"] = request.ParentId,
            ["ancestorIds"] = ancestorIds,
            ["name"] = request.Title.Trim(),
            ["title"] = request.Title.Trim(),
            ["description"] = request.Description,
            ["tags"] = request.Tags ?? new List<string>(),
            ["content"] = request.Content,
            ["contentType"] = ResourceContentType.Markdown,
            ["extension"] = "md",
            ["mimeType"] = "text/markdown",
            ["size"] = (long)(request.Content?.Length ?? 0),
            ["currentVersionNumber"] = 1,
            ["status"] = request.IsDraft ? ResourceStatus.Draft : ResourceStatus.Published
        };

        var created = await _dg.CreateAsync<DmResource>(DmDatasets.Resources, payload, Token, ct);
        await WriteVersionAsync(created.__dataId!, 1, request.Content ?? string.Empty, "initial", ct);
        return await ToDtoWithEffectiveAsync(created, ct);
    }

    public async Task<ResourceDto> UpdateMarkdownAsync(string id, UpdateMarkdownRequest request, CancellationToken ct = default)
    {
        ValidateContentLength(request.Content);
        var existing = await LoadOrThrowAsync(id, ct);

        if (existing.type != ResourceType.Markdown)
            throw DocumentException.Validation("NOT_MARKDOWN", "Resource is not a markdown document.", "Kaynak bir markdown doküman değil.");

        var snapshot = await _perms.LoadSnapshotAsync(ct);
        snapshot.EnsureCan(existing, ResourceAction.Edit);

        var currentVersion = existing.currentVersionNumber ?? 1;
        if (request.ExpectedVersionNumber != currentVersion)
        {
            throw DocumentException.Conflict(
                "VERSION_CONFLICT",
                $"Document was modified by someone else (server v{currentVersion}, you sent v{request.ExpectedVersionNumber}).",
                "Doküman başka biri tarafından güncellenmiş. Lütfen yenileyip tekrar deneyin.");
        }

        var newVersion = currentVersion + 1;
        var payload = new Dictionary<string, object?>
        {
            ["content"] = request.Content,
            ["currentVersionNumber"] = newVersion,
            ["size"] = (long)(request.Content?.Length ?? 0)
        };
        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            payload["name"] = request.Title.Trim();
            payload["title"] = request.Title.Trim();
        }
        if (request.Description != null)
            payload["description"] = request.Description;
        if (request.Tags != null)
            payload["tags"] = request.Tags;
        if (request.IsDraft.HasValue)
            payload["status"] = request.IsDraft.Value ? ResourceStatus.Draft : ResourceStatus.Published;

        var updated = await _dg.UpdateAsync<DmResource>(DmDatasets.Resources, id, payload, Token, ct);
        await WriteVersionAsync(id, newVersion, request.Content ?? string.Empty, ResolveChangeNote(request.ChangeNote, "update"), ct);
        return ToDto(updated, snapshot.Resolve(updated));
    }

    public async Task<MarkdownContentDto> GetMarkdownContentAsync(string id, CancellationToken ct = default)
    {
        var resource = await LoadOrThrowAsync(id, ct);
        if (resource.type != ResourceType.Markdown)
            throw DocumentException.Validation("NOT_MARKDOWN", "Resource is not a markdown document.", "Kaynak bir markdown doküman değil.");

        var snapshot = await _perms.LoadSnapshotAsync(ct);
        snapshot.EnsureCan(resource, ResourceAction.View);

        return new MarkdownContentDto
        {
            Id = resource.__dataId!,
            Title = resource.title,
            Content = resource.content ?? string.Empty,
            CurrentVersionNumber = resource.currentVersionNumber ?? 1
        };
    }

    public async Task<IReadOnlyList<MarkdownVersionDto>> GetMarkdownVersionsAsync(string id, CancellationToken ct = default)
    {
        var resource = await LoadOrThrowAsync(id, ct);
        if (resource.type != ResourceType.Markdown)
            throw DocumentException.Validation("NOT_MARKDOWN", "Resource is not a markdown document.", "Kaynak bir markdown doküman değil.");

        var snapshot = await _perms.LoadSnapshotAsync(ct);
        snapshot.EnsureCan(resource, ResourceAction.View);

        var currentVersion = resource.currentVersionNumber ?? 1;
        var auditMap = BuildVersionAuditMap(resource.__history);
        var match = new Dictionary<string, object?> { ["resourceId"] = id };
        var page = await _dg.QueryPageAsync(DmDatasets.ResourceVersions, match, ListQuery, Token, ct);

        return page.Items
            .Select(MapVersionRow)
            .OrderByDescending(v => v.versionNumber ?? 0)
            .Select(v =>
            {
                auditMap.TryGetValue(v.versionNumber ?? 0, out var audit);
                return new MarkdownVersionDto
                {
                    VersionNumber = v.versionNumber ?? 0,
                    ChangeNote = v.changeNote,
                    Size = v.size,
                    CreatedAt = v.createdAt ?? CreatedFrom(v.__history)?.timestamp ?? audit?.timestamp,
                    CreatedBy = v.createdBy ?? CreatedFrom(v.__history)?.userEmail ?? audit?.userEmail,
                    IsCurrent = (v.versionNumber ?? 0) == currentVersion
                };
            })
            .ToList();
    }

    /// <summary>
    /// Kaynak <c>__history</c>'sinden sürüm numarası → audit girdisi haritası kurar.
    /// <c>create</c> → v1; her <c>update</c> için <c>changes.currentVersionNumber</c> hedef sürümdür.
    /// Eski sürümlerin (açık audit gömülmeden önce yazılan) yazar/tarihini telafi eder.
    /// </summary>
    private static Dictionary<int, DmHistoryEntry> BuildVersionAuditMap(List<DmHistoryEntry>? history)
    {
        var map = new Dictionary<int, DmHistoryEntry>();
        if (history is null)
            return map;

        foreach (var entry in history)
        {
            if (string.Equals(entry.operation, "create", StringComparison.OrdinalIgnoreCase))
            {
                map[1] = entry;
            }
            else if (string.Equals(entry.operation, "update", StringComparison.OrdinalIgnoreCase)
                && entry.changes is not null
                && entry.changes.TryGetValue("currentVersionNumber", out var cv)
                && cv.ValueKind == JsonValueKind.Number
                && cv.TryGetInt32(out var versionNumber))
            {
                map[versionNumber] = entry;
            }
        }

        return map;
    }

    public async Task<MarkdownVersionContentDto> GetMarkdownVersionContentAsync(string id, int versionNumber, CancellationToken ct = default)
    {
        var resource = await LoadOrThrowAsync(id, ct);
        var snapshot = await _perms.LoadSnapshotAsync(ct);
        snapshot.EnsureCan(resource, ResourceAction.View);

        var version = await LoadVersionOrThrowAsync(id, versionNumber, ct);
        DmHistoryEntry? audit = null;
        if (version.createdAt is null && version.createdBy is null)
        {
            BuildVersionAuditMap(resource.__history).TryGetValue(versionNumber, out audit);
        }

        return new MarkdownVersionContentDto
        {
            VersionNumber = version.versionNumber ?? versionNumber,
            Content = version.contentSnapshot ?? string.Empty,
            ChangeNote = version.changeNote,
            CreatedAt = version.createdAt ?? CreatedFrom(version.__history)?.timestamp ?? audit?.timestamp,
            CreatedBy = version.createdBy ?? CreatedFrom(version.__history)?.userEmail ?? audit?.userEmail
        };
    }

    public async Task<ResourceDto> RestoreMarkdownVersionAsync(string id, int versionNumber, CancellationToken ct = default)
    {
        var existing = await LoadOrThrowAsync(id, ct);
        if (existing.type != ResourceType.Markdown)
            throw DocumentException.Validation("NOT_MARKDOWN", "Resource is not a markdown document.", "Kaynak bir markdown doküman değil.");

        var snapshot = await _perms.LoadSnapshotAsync(ct);
        snapshot.EnsureCan(existing, ResourceAction.Edit);

        var version = await LoadVersionOrThrowAsync(id, versionNumber, ct);
        var content = version.contentSnapshot ?? string.Empty;

        var newVersion = (existing.currentVersionNumber ?? 1) + 1;
        var payload = new Dictionary<string, object?>
        {
            ["content"] = content,
            ["currentVersionNumber"] = newVersion,
            ["size"] = (long)content.Length
        };

        var updated = await _dg.UpdateAsync<DmResource>(DmDatasets.Resources, id, payload, Token, ct);
        await WriteVersionAsync(id, newVersion, content, $"restore from v{versionNumber}", ct);
        return ToDto(updated, snapshot.Resolve(updated));
    }

    public async Task<ResourceDto> CreateFileResourceAsync(CreateFileResourceRequest request, CancellationToken ct = default)
    {
        ValidateName(request.Name);
        if (string.IsNullOrWhiteSpace(request.Content))
            throw DocumentException.Validation("CONTENT_REQUIRED", "File content is required.", "Dosya içeriği zorunludur.");

        await EnsureCanOnParentAsync(request.ParentId, ResourceAction.Upload, ct);
        var ancestorIds = await ResolveAncestorsForChildAsync(request.ParentId, ct);

        // DG dm_resources.file (fieldType=file) alanı: { content (base64), originalFileName } verildiğinde
        // DataController dosyayı MinIO'ya yükler ve alanı { path, file_name, file_ext, file_size, ... } ile değiştirir.
        var filePayload = new Dictionary<string, object?>
        {
            ["content"] = request.Content,
            ["originalFileName"] = string.IsNullOrWhiteSpace(request.OriginalFileName) ? request.Name.Trim() : request.OriginalFileName
        };

        var payload = new Dictionary<string, object?>
        {
            ["type"] = ResourceType.File,
            ["parentId"] = request.ParentId,
            ["ancestorIds"] = ancestorIds,
            ["name"] = request.Name.Trim(),
            ["title"] = request.Name.Trim(),
            ["description"] = request.Description,
            ["tags"] = request.Tags ?? new List<string>(),
            ["contentType"] = ResourceContentType.Binary,
            ["mimeType"] = request.MimeType,
            ["extension"] = request.Extension,
            ["size"] = request.Size,
            ["file"] = filePayload,
            ["currentVersionNumber"] = 1
        };

        if (!string.IsNullOrWhiteSpace(request.Origin))
            payload["origin"] = request.Origin.Trim();
        if (!string.IsNullOrWhiteSpace(request.TemplateId))
            payload["templateId"] = request.TemplateId.Trim();
        if (!string.IsNullOrWhiteSpace(request.TemplateCode))
            payload["templateCode"] = request.TemplateCode.Trim();
        if (!string.IsNullOrWhiteSpace(request.GenerationProfile))
            payload["generationProfile"] = request.GenerationProfile.Trim();
        if (!string.IsNullOrWhiteSpace(request.LetterheadId))
            payload["letterheadId"] = request.LetterheadId.Trim();
        if (!string.IsNullOrWhiteSpace(request.DocumentNo))
            payload["documentNo"] = request.DocumentNo.Trim();

        var created = await _dg.CreateAsync<DmResource>(DmDatasets.Resources, payload, Token, ct);
        return await ToDtoWithEffectiveAsync(created, ct);
    }

    public async Task<ResourceListResult> SearchAsync(string query, int skip, int limit, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new ResourceListResult { Items = Array.Empty<ResourceDto>(), Total = 0 };

        var safeLimit = Math.Clamp(limit <= 0 ? 50 : limit, 1, 1000);
        var safeSkip = Math.Max(0, skip);
        var queryString = $"skip={safeSkip}&limit={safeLimit}&expand=false&showHistory=true&search={Uri.EscapeDataString(query.Trim())}";

        var page = await _dg.QueryPageAsync(DmDatasets.Resources, new Dictionary<string, object?>(), queryString, Token, ct);
        var snapshot = await _perms.LoadSnapshotAsync(ct);
        var items = page.Items
            .Select(MapRow)
            .Where(r => snapshot.Resolve(r).CanView)
            .Where(r => r.type != ResourceType.Markdown || ResourceStatus.Normalize(r.status) == ResourceStatus.Published)
            .Select(r => ToDto(r, snapshot.Resolve(r)))
            .ToList();
        return new ResourceListResult { Items = items, Total = items.Count };
    }

    public async Task<ResourceListResult> GetRecentAsync(int limit, CancellationToken ct = default)
    {
        var safeLimit = Math.Clamp(limit <= 0 ? 10 : limit, 1, 100);
        var snapshot = await _perms.LoadSnapshotAsync(ct);
        var match = new Dictionary<string, object?> { ["type"] = ResourceType.Markdown };
        var page = await _dg.QueryPageAsync(DmDatasets.Resources, match, ListQuery, Token, ct);
        var items = page.Items
            .Select(MapRow)
            .Where(r => snapshot.Resolve(r).CanView)
            .Where(r => ResourceStatus.Normalize(r.status) == ResourceStatus.Published)
            .Select(r => ToDto(r, snapshot.Resolve(r)))
            .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt ?? DateTime.MinValue)
            .Take(safeLimit)
            .ToList();
        return new ResourceListResult { Items = items, Total = items.Count };
    }

    public async Task<ResourceListResult> GetDraftsAsync(int limit, CancellationToken ct = default)
    {
        var safeLimit = Math.Clamp(limit <= 0 ? 50 : limit, 1, 200);
        var snapshot = await _perms.LoadSnapshotAsync(ct);
        var match = new Dictionary<string, object?>
        {
            ["type"] = ResourceType.Markdown,
            ["status"] = ResourceStatus.Draft,
        };
        var page = await _dg.QueryPageAsync(DmDatasets.Resources, match, ListQuery, Token, ct);
        var allDrafts = page.Items
            .Select(MapRow)
            .Where(r => snapshot.Resolve(r).CanEdit)
            .Select(r => ToDto(r, snapshot.Resolve(r)))
            .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt ?? DateTime.MinValue)
            .ToList();
        return new ResourceListResult
        {
            Items = allDrafts.Take(safeLimit).ToList(),
            Total = allDrafts.Count,
        };
    }

    public async Task<ResourceListResult> GetMarkdownBacklinksAsync(string id, CancellationToken ct = default)
    {
        var rid = id?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rid))
            throw DocumentException.NotFound();

        var target = await LoadOrThrowAsync(rid, ct);
        if (target.type != ResourceType.Markdown)
        {
            throw DocumentException.Validation(
                "NOT_MARKDOWN",
                "Resource is not a markdown document.",
                "Kaynak bir markdown doküman değil.");
        }

        var snapshot = await _perms.LoadSnapshotAsync(ct);
        snapshot.EnsureCan(target, ResourceAction.View);

        var candidates = await QueryAllMarkdownAsync(ct);
        var items = candidates
            .Where(r => !string.Equals(r.__dataId, rid, StringComparison.OrdinalIgnoreCase))
            .Where(r => MarkdownLinkHelper.ContentLinksToResource(r.content, rid))
            .Where(r => snapshot.Resolve(r).CanView)
            .Select(r => ToDto(r, snapshot.Resolve(r)))
            .OrderBy(r => r.Title ?? r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ResourceListResult { Items = items, Total = items.Count };
    }

    // ----- helpers -----

    private static string ResolveChangeNote(string? userNote, string fallback)
    {
        var trimmed = userNote?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return fallback;

        const int maxLen = 500;
        return trimmed.Length <= maxLen ? trimmed : trimmed[..maxLen];
    }

    private async Task<List<DmResource>> QueryAllMarkdownAsync(CancellationToken ct)
    {
        const int pageSize = 500;
        const int maxRows = 10_000;
        var all = new List<DmResource>();
        var skip = 0;

        while (skip < maxRows)
        {
            var match = new Dictionary<string, object?> { ["type"] = ResourceType.Markdown };
            var query = $"limit={pageSize}&skip={skip}&expand=false&showHistory=false";
            var page = await _dg.QueryPageAsync(DmDatasets.Resources, match, query, Token, ct);
            if (page.Items.Count == 0)
                break;

            all.AddRange(page.Items.Select(MapRow));
            if (page.Items.Count < pageSize)
                break;

            skip += pageSize;
        }

        return all;
    }

    private static string? NormalizeParentId(string? parentId) =>
        string.IsNullOrWhiteSpace(parentId) ? null : parentId;

    private static IReadOnlyList<TreeNodeDto> BuildTreeFromSnapshot(PermissionSnapshot snapshot)
    {
        var folders = snapshot.AllFolders
            .Where(f => snapshot.Resolve(f).CanView)
            .ToList();
        return BuildTree(folders);
    }

    private async Task<ResourceListResult> QueryChildrenAsync(
        string? parentId,
        PermissionSnapshot snapshot,
        CancellationToken ct)
    {
        var match = new Dictionary<string, object?> { ["parentId"] = parentId };
        var page = await _dg.QueryPageAsync(DmDatasets.Resources, match, ListQuery, Token, ct);

        var items = page.Items
            .Select(MapRow)
            .Where(r => snapshot.Resolve(r).CanView)
            .OrderByDescending(r => r.type == ResourceType.Folder)
            .ThenBy(r => r.name, StringComparer.OrdinalIgnoreCase)
            .Select(r => ToDto(r, snapshot.Resolve(r)))
            .ToList();

        return new ResourceListResult { Items = items, Total = items.Count };
    }

    private async Task<(IReadOnlyList<BreadcrumbDto> Breadcrumb, ResourceDto? SelectedFolder)> ResolveFolderContextAsync(
        string? folderId,
        PermissionSnapshot snapshot,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(folderId))
            return (Array.Empty<BreadcrumbDto>(), null);

        var resource = await LoadOrThrowAsync(folderId, ct);
        snapshot.EnsureCan(resource, ResourceAction.View);
        var breadcrumb = await BuildBreadcrumbAsync(resource, ct);
        return (breadcrumb, ToDto(resource, snapshot.Resolve(resource)));
    }

    private async Task<IReadOnlyList<BreadcrumbDto>> BuildBreadcrumbAsync(DmResource resource, CancellationToken ct)
    {
        var ancestorIds = resource.ancestorIds ?? new List<string>();
        var crumbs = new List<BreadcrumbDto>();
        if (ancestorIds.Count > 0)
        {
            var match = new Dictionary<string, object?>
            {
                ["__dataId"] = new Dictionary<string, object?> { ["$in"] = ancestorIds }
            };
            var page = await _dg.QueryPageAsync(DmDatasets.Resources, match, ListQuery, Token, ct);
            var byId = page.Items.Select(MapRow).Where(r => r.__dataId != null).ToDictionary(r => r.__dataId!, r => r);

            foreach (var ancestorId in ancestorIds)
            {
                if (byId.TryGetValue(ancestorId, out var ancestor))
                    crumbs.Add(new BreadcrumbDto { Id = ancestorId, Name = ancestor.name ?? string.Empty });
            }
        }

        crumbs.Add(new BreadcrumbDto { Id = resource.__dataId!, Name = resource.name ?? string.Empty });
        return crumbs;
    }

    private async Task<DmResource> LoadOrThrowAsync(string id, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw DocumentException.NotFound();

        var resource = await _dg.GetByIdAsync<DmResource>(DmDatasets.Resources, id, Token, ct);
        if (resource is null || resource.__dataId is null)
            throw DocumentException.NotFound();

        return resource;
    }

    /// <summary>Hedef klasörde (oluşturma/yükleme için) yetki zorlar. Kök (parentId boş) açık varsayılandır.</summary>
    private async Task EnsureCanOnParentAsync(string? parentId, string action, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(parentId))
            return;

        var parent = await LoadOrThrowAsync(parentId, ct);
        var snapshot = await _perms.LoadSnapshotAsync(ct);
        snapshot.EnsureCan(parent, action);
    }

    /// <summary>Oluşturma/güncelleme sonrası dönen kaynak için geçerli kullanıcının etkin yetkisini çözer.</summary>
    private async Task<ResourceDto> ToDtoWithEffectiveAsync(DmResource r, CancellationToken ct)
    {
        var snapshot = await _perms.LoadSnapshotAsync(ct);
        return ToDto(r, snapshot.Resolve(r));
    }

    private async Task<List<string>> ResolveAncestorsForChildAsync(string? parentId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(parentId))
            return new List<string>();

        var parent = await LoadOrThrowAsync(parentId, ct);
        if (parent.type != ResourceType.Folder)
            throw DocumentException.Validation("INVALID_PARENT", "Parent must be a folder.", "Üst kaynak yalnızca klasör olabilir.");

        return new List<string>(parent.ancestorIds ?? new List<string>()) { parentId };
    }

    private async Task<List<DmResource>> GetDescendantsAsync(string id, CancellationToken ct)
    {
        var match = new Dictionary<string, object?> { ["ancestorIds"] = id };
        var page = await _dg.QueryPageAsync(DmDatasets.Resources, match, ListQuery, Token, ct);
        return page.Items.Select(MapRow).ToList();
    }

    private async Task ReindexDescendantsAsync(string movedNodeId, List<string> movedNodeNewAncestors, CancellationToken ct)
    {
        var descendants = await GetDescendantsAsync(movedNodeId, ct);
        if (descendants.Count == 0)
            return;

        var prefix = new List<string>(movedNodeNewAncestors) { movedNodeId };

        foreach (var descendant in descendants)
        {
            if (descendant.__dataId is null)
                continue;

            var oldAncestors = descendant.ancestorIds ?? new List<string>();
            var pivot = oldAncestors.IndexOf(movedNodeId);
            var subPath = pivot >= 0 ? oldAncestors.Skip(pivot + 1) : Enumerable.Empty<string>();
            var newAncestors = prefix.Concat(subPath).ToList();

            var payload = new Dictionary<string, object?> { ["ancestorIds"] = newAncestors };
            await _dg.UpdateAsync<DmResource>(DmDatasets.Resources, descendant.__dataId, payload, Token, ct);
        }
    }

    private async Task DeleteResourceAndVersionsAsync(string id, string? type, CancellationToken ct)
    {
        var match = new Dictionary<string, object?> { ["resourceId"] = id };
        var versions = await _dg.QueryPageAsync(DmDatasets.ResourceVersions, match, ListQuery, Token, ct);
        foreach (var version in versions.Items)
        {
            if (version.TryGetValue("__dataId", out var versionId) && versionId is not null)
            {
                var vid = GetString(versionId);
                if (!string.IsNullOrEmpty(vid))
                    await _dg.DeleteAsync(DmDatasets.ResourceVersions, vid, Token, ct);
            }
        }

        // Klasörlerde bağlı grup izin kayıtlarını da temizle (yalnızca anchor'larda olur).
        if (type == ResourceType.Folder)
            await _perms.DeleteFolderPermissionsAsync(id, ct);

        await _dg.DeleteAsync(DmDatasets.Resources, id, Token, ct);
    }

    private async Task WriteVersionAsync(string resourceId, int versionNumber, string content, string changeNote, CancellationToken ct)
    {
        var payload = new Dictionary<string, object?>
        {
            ["resourceId"] = resourceId,
            ["versionNumber"] = versionNumber,
            ["changeNote"] = changeNote,
            ["contentSnapshot"] = content,
            ["size"] = (long)content.Length,
            ["mimeType"] = "text/markdown",
            // dm_resource_versions dataset'inde DG logging kapalı; audit'i açıkça gömüyoruz.
            ["createdBy"] = _ctx.Username,
            ["createdAt"] = DateTime.UtcNow
        };

        try
        {
            await _dg.CreateAsync<Dictionary<string, object?>>(DmDatasets.ResourceVersions, payload, Token, ct);
        }
        catch (Exception ex)
        {
            // Versiyon kaydı yan etkidir; ana işlemi düşürmemek için loglanır (Faz 1: telafi yok).
            _logger.LogWarning(ex, "Failed to write version {Version} for resource {ResourceId}", versionNumber, resourceId);
        }
    }

    private static List<TreeNodeDto> BuildTree(IReadOnlyList<DmResource> folders)
    {
        var nodes = folders
            .Where(f => f.__dataId is not null)
            .ToDictionary(
                f => f.__dataId!,
                f => new TreeNodeDto { Id = f.__dataId!, Name = f.name ?? string.Empty, ParentId = f.parentId });

        var roots = new List<TreeNodeDto>();
        foreach (var folder in folders)
        {
            if (folder.__dataId is null)
                continue;

            var node = nodes[folder.__dataId];
            if (!string.IsNullOrWhiteSpace(folder.parentId) && nodes.TryGetValue(folder.parentId!, out var parent))
                parent.Children.Add(node);
            else
                roots.Add(node);
        }

        SortTree(roots);
        return roots;
    }

    private static void SortTree(List<TreeNodeDto> nodes)
    {
        nodes.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        foreach (var node in nodes)
            SortTree(node.Children);
    }

    private static DmResource MapRow(Dictionary<string, object?> row)
    {
        var json = JsonSerializer.Serialize(row, JsonOptions);
        return JsonSerializer.Deserialize<DmResource>(json, JsonOptions) ?? new DmResource();
    }

    private static DmResourceVersion MapVersionRow(Dictionary<string, object?> row)
    {
        var json = JsonSerializer.Serialize(row, JsonOptions);
        return JsonSerializer.Deserialize<DmResourceVersion>(json, JsonOptions) ?? new DmResourceVersion();
    }

    /// <summary>Oluşturma audit girdisi: ilk <c>create</c>, yoksa ilk girdi.</summary>
    private static DmHistoryEntry? CreatedFrom(List<DmHistoryEntry>? history)
    {
        if (history is null || history.Count == 0)
            return null;
        return history.FirstOrDefault(h => string.Equals(h.operation, "create", StringComparison.OrdinalIgnoreCase))
            ?? history[0];
    }

    /// <summary>Son güncelleme audit girdisi: en son <c>update</c>; hiç güncelleme yoksa null.</summary>
    private static DmHistoryEntry? UpdatedFrom(List<DmHistoryEntry>? history)
    {
        if (history is null || history.Count == 0)
            return null;
        return history.LastOrDefault(h => string.Equals(h.operation, "update", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<DmResourceVersion> LoadVersionOrThrowAsync(string id, int versionNumber, CancellationToken ct)
    {
        var match = new Dictionary<string, object?>
        {
            ["resourceId"] = id,
            ["versionNumber"] = versionNumber
        };
        var page = await _dg.QueryPageAsync(DmDatasets.ResourceVersions, match, ListQuery, Token, ct);
        var version = page.Items.Select(MapVersionRow).FirstOrDefault();
        if (version is null)
            throw DocumentException.NotFound();
        return version;
    }

    private static ResourceDto ToDto(DmResource r, EffectivePermissionDto? effective = null)
    {
        var (filePath, fileName) = ReadFileField(r.file);
        return new ResourceDto
        {
            Permissions = effective ?? EffectivePermissionDto.Full,
            Id = r.__dataId ?? string.Empty,
            Type = r.type ?? string.Empty,
            ParentId = r.parentId,
            AncestorIds = r.ancestorIds ?? new List<string>(),
            Name = r.name ?? string.Empty,
            Title = r.title,
            Description = r.description,
            Tags = r.tags ?? new List<string>(),
            ContentType = r.contentType,
            MimeType = r.mimeType,
            Extension = r.extension,
            Size = r.size,
            CurrentVersionNumber = r.currentVersionNumber ?? 1,
            HasContent = (r.type == ResourceType.Markdown && !string.IsNullOrEmpty(r.content))
                || (r.type == ResourceType.File && !string.IsNullOrEmpty(filePath)),
            Status = ResourceStatus.Normalize(r.status),
            FilePath = filePath,
            FileName = fileName,
            Origin = r.origin,
            TemplateId = r.templateId,
            TemplateCode = r.templateCode,
            GenerationProfile = r.generationProfile,
            LetterheadId = r.letterheadId,
            DocumentNo = r.documentNo,
            CreatedAt = CreatedFrom(r.__history)?.timestamp,
            CreatedBy = CreatedFrom(r.__history)?.userEmail,
            UpdatedAt = UpdatedFrom(r.__history)?.timestamp,
            UpdatedBy = UpdatedFrom(r.__history)?.userEmail
        };
    }

    /// <summary>DG <c>file</c> alanından (stored object: path/file_name) path ve dosya adını çıkarır.</summary>
    private static (string? Path, string? Name) ReadFileField(JsonElement? file)
    {
        if (file is null || file.Value.ValueKind != JsonValueKind.Object)
            return (null, null);

        string? path = file.Value.TryGetProperty("path", out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;
        string? name = file.Value.TryGetProperty("file_name", out var n) && n.ValueKind == JsonValueKind.String
            ? n.GetString()
            : null;

        return (string.IsNullOrWhiteSpace(path) ? null : path, string.IsNullOrWhiteSpace(name) ? null : name);
    }

    private static void ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw DocumentException.Validation("NAME_REQUIRED", "Name is required.", "İsim zorunludur.");
    }

    private void ValidateContentLength(string? content)
    {
        if ((content?.Length ?? 0) > _settings.Resources.MaxMarkdownContentLength)
        {
            throw DocumentException.Validation(
                "CONTENT_TOO_LARGE",
                $"Markdown content exceeds the {_settings.Resources.MaxMarkdownContentLength} character limit.",
                "Markdown içeriği izin verilen boyut sınırını aşıyor.");
        }
    }

    private static string? GetString(object? value) => value switch
    {
        null => null,
        string s => s,
        JsonElement { ValueKind: JsonValueKind.String } el => el.GetString(),
        _ => value.ToString()
    };
}
