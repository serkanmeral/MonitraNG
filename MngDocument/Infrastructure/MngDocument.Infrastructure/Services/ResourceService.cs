using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngDocument.Application.Configuration;
using MngDocument.Application.Contracts.Letterheads;
using MngDocument.Application.Contracts.Resources;
using MngDocument.Application.Contracts.Templates;
using MngDocument.Application.Exceptions;
using MngDocument.Application.Interfaces;
using MngDocument.Application.Models;
using MngDocument.Application.Utilities;
using MngDocument.Domain.Constants;
using MngDocument.Infrastructure.Services.Generation;

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
    private const string TreeFolderListQuery = "limit=500&expand=false&showHistory=false&sort=name";
    private const string ChildrenListQuerySuffix = "expand=false&showHistory=false";
    private const string DocxMime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private readonly Dictionary<string, List<ResourceDto>> _visibleChildrenCache = new(StringComparer.Ordinal);

    private readonly IMngDataGatewayClient _dg;
    private readonly IRequestContext _ctx;
    private readonly IPermissionService _perms;
    private readonly ILetterheadService _letterheads;
    private readonly ITagService _tags;
    private readonly ITemplateBrandingApplier _brandingApplier;
    private readonly LetterheadHeaderValueEnricher _headerEnricher;
    private readonly IDocumentRenderService _render;
    private readonly MngDocumentSettings _settings;
    private readonly ILogger<ResourceService> _logger;

    public ResourceService(
        IMngDataGatewayClient dg,
        IRequestContext ctx,
        IPermissionService perms,
        ILetterheadService letterheads,
        ITagService tags,
        ITemplateBrandingApplier brandingApplier,
        LetterheadHeaderValueEnricher headerEnricher,
        IDocumentRenderService render,
        IOptions<MngDocumentSettings> settings,
        ILogger<ResourceService> logger)
    {
        _dg = dg;
        _ctx = ctx;
        _perms = perms;
        _letterheads = letterheads;
        _tags = tags;
        _brandingApplier = brandingApplier;
        _headerEnricher = headerEnricher;
        _render = render;
        _settings = settings.Value;
        _logger = logger;
    }

    private string? Token => _ctx.BearerToken;

    public async Task<IReadOnlyList<TreeNodeDto>> GetTreeAsync(CancellationToken ct = default)
    {
        var snapshot = await _perms.LoadSnapshotAsync(ct, PermissionSnapshotScope.Full);
        return BuildTreeFromSnapshot(snapshot);
    }

    public Task<IReadOnlyList<TreeNodeDto>> GetTreeRootsAsync(CancellationToken ct = default) =>
        QueryLazyTreeFolderNodesAsync(null, ct);

    public Task<IReadOnlyList<TreeNodeDto>> GetTreeChildrenAsync(string? parentId, CancellationToken ct = default) =>
        QueryLazyTreeFolderNodesAsync(NormalizeParentId(parentId), ct);

    public async Task<TreePathDto> GetTreePathAsync(string folderId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(folderId))
        {
            throw DocumentException.Validation(
                "FOLDER_ID_REQUIRED",
                "Folder id is required.",
                "Klasör id gerekli.");
        }

        var resource = await LoadOrThrowAsync(folderId.Trim(), ct);
        if (!string.Equals(resource.type, ResourceType.Folder, StringComparison.OrdinalIgnoreCase))
        {
            throw DocumentException.Validation(
                "NOT_A_FOLDER",
                "Resource is not a folder.",
                "Kaynak bir klasör değil.");
        }

        var snapshot = await _perms.LoadSnapshotAsync(ct);
        snapshot.EnsureCan(resource, ResourceAction.View);

        var breadcrumb = await BuildBreadcrumbAsync(resource, ct);
        var segments = new List<TreePathSegmentDto> { new() { ParentId = null, Nodes = await QueryLazyTreeFolderNodesAsync(null, snapshot, ct) } };

        foreach (var crumb in breadcrumb)
        {
            segments.Add(new TreePathSegmentDto
            {
                ParentId = crumb.Id,
                Nodes = await QueryLazyTreeFolderNodesAsync(crumb.Id, snapshot, ct)
            });
        }

        return new TreePathDto
        {
            Breadcrumb = breadcrumb,
            Segments = segments
        };
    }

    public async Task<ResourceBootstrapDto> GetBootstrapAsync(
        string? folderId = null,
        int skip = 0,
        int? limit = null,
        CancellationToken ct = default)
    {
        var snapshot = await _perms.LoadSnapshotAsync(ct);
        var treeRoots = await QueryLazyTreeFolderNodesAsync(null, snapshot, ct);
        var children = await QueryChildrenAsync(NormalizeParentId(folderId), snapshot, ct, skip, limit);
        var (breadcrumb, selectedFolder) = await ResolveFolderContextAsync(folderId, snapshot, ct);

        return new ResourceBootstrapDto
        {
            TreeRoots = treeRoots,
            Tree = Array.Empty<TreeNodeDto>(),
            Children = children,
            Breadcrumb = breadcrumb,
            SelectedFolder = selectedFolder
        };
    }

    public async Task<ResourceBrowseContextDto> GetBrowseContextAsync(
        string? folderId,
        int skip = 0,
        int? limit = null,
        CancellationToken ct = default)
    {
        var snapshot = await _perms.LoadSnapshotAsync(ct);
        var children = await QueryChildrenAsync(NormalizeParentId(folderId), snapshot, ct, skip, limit);
        var (breadcrumb, selectedFolder) = await ResolveFolderContextAsync(folderId, snapshot, ct);

        return new ResourceBrowseContextDto
        {
            Children = children,
            Breadcrumb = breadcrumb,
            SelectedFolder = selectedFolder
        };
    }

    public async Task<ResourceListResult> GetChildrenAsync(
        string? parentId,
        int skip = 0,
        int? limit = null,
        CancellationToken ct = default)
    {
        var snapshot = await _perms.LoadSnapshotAsync(ct);
        return await QueryChildrenAsync(NormalizeParentId(parentId), snapshot, ct, skip, limit);
    }

    public async Task<IReadOnlyList<TreeNodeDto>> SearchTreeFoldersAsync(
        string query,
        int limit = 50,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<TreeNodeDto>();

        var safeLimit = Math.Clamp(limit <= 0 ? 50 : limit, 1, _settings.Resources.MaxChildrenPageSize);
        var snapshot = await _perms.LoadSnapshotAsync(ct);
        var match = new Dictionary<string, object?> { ["type"] = ResourceType.Folder };
        var queryString =
            $"limit={safeLimit}&skip=0&{ChildrenListQuerySuffix}&search={Uri.EscapeDataString(query.Trim())}";
        var page = await _dg.QueryPageAsync(DmDatasets.Resources, match, queryString, Token, ct);

        var folders = page.Items
            .Select(MapRow)
            .Where(f => f.__dataId is not null && snapshot.Resolve(f).CanView)
            .OrderBy(f => f.name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var hasChildren = await ResolveFolderHasChildrenAsync(
            folders.Select(f => f.__dataId!).ToList(),
            snapshot,
            ct);

        return folders
            .Select(f => new TreeNodeDto
            {
                Id = f.__dataId!,
                Name = f.name ?? string.Empty,
                ParentId = f.parentId,
                HasChildren = hasChildren.Contains(f.__dataId!),
                Children = new List<TreeNodeDto>()
            })
            .ToList();
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
        var tags = await ResolveTagsAsync(request.Tags, ct);

        var payload = new Dictionary<string, object?>
        {
            ["type"] = ResourceType.Folder,
            ["parentId"] = request.ParentId,
            ["ancestorIds"] = ancestorIds,
            ["name"] = request.Name.Trim(),
            ["title"] = request.Name.Trim(),
            ["description"] = request.Description,
            ["tags"] = tags,
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

    public async Task<ResourceDto> UpdateMetadataAsync(string id, UpdateResourceMetadataRequest request, CancellationToken ct = default)
    {
        var resource = await LoadOrThrowAsync(id, ct);
        var snapshot = await _perms.LoadSnapshotAsync(ct);
        snapshot.EnsureCan(resource, ResourceAction.Edit);

        var payload = new Dictionary<string, object?>();
        if (request.Tags is not null)
            payload["tags"] = await ResolveTagsAsync(request.Tags, ct);
        if (request.Description is not null)
            payload["description"] = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        if (payload.Count == 0)
            return ToDto(resource, snapshot.Resolve(resource));

        var updated = await _dg.UpdateAsync<DmResource>(DmDatasets.Resources, id, payload, Token, ct);
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
        var tags = await ResolveTagsAsync(request.Tags, ct);

        var payload = new Dictionary<string, object?>
        {
            ["type"] = ResourceType.Markdown,
            ["parentId"] = request.ParentId,
            ["ancestorIds"] = ancestorIds,
            ["name"] = request.Title.Trim(),
            ["title"] = request.Title.Trim(),
            ["description"] = request.Description,
            ["tags"] = tags,
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
            payload["tags"] = await ResolveTagsAsync(request.Tags, ct);
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

        var version = await LoadVersionOrThrowAsync(id, versionNumber, null, ct);
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

        var version = await LoadVersionOrThrowAsync(id, versionNumber, null, ct);
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

    public async Task<IReadOnlyList<MarkdownVersionDto>> GetFileVersionsAsync(string id, CancellationToken ct = default)
    {
        var resource = await LoadOrThrowAsync(id, ct);
        EnsureManagedOfficeFile(resource);

        var snapshot = await _perms.LoadSnapshotAsync(ct);
        snapshot.EnsureCan(resource, ResourceAction.View);

        return await ListVersionDtosAsync(resource, ct);
    }

    public async Task<(byte[] Content, string FileName)> GetFileVersionContentAsync(
        string id,
        int versionNumber,
        CancellationToken ct = default)
    {
        var resource = await LoadOrThrowAsync(id, ct);
        EnsureManagedOfficeFile(resource);

        var snapshot = await _perms.LoadSnapshotAsync(ct);
        snapshot.EnsureCan(resource, ResourceAction.View);

        return await ReadFileVersionBytesAsync(resource, versionNumber, null, ct);
    }

    public async Task<(byte[] Content, string FileName)> GetFileVersionContentForEditorAsync(
        string id,
        int versionNumber,
        string dataGatewayToken,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dataGatewayToken))
        {
            throw DocumentException.Validation(
                "AUTH_REQUIRED",
                "Bearer token is required.",
                "Oturum doğrulaması gerekli.");
        }

        var resource = await _dg.GetByIdAsync<DmResource>(DmDatasets.Resources, id, dataGatewayToken, ct);
        if (resource is null || resource.__dataId is null)
            throw DocumentException.NotFound("Dosya bulunamadı.");

        EnsureManagedOfficeFile(resource);
        return await ReadFileVersionBytesAsync(resource, versionNumber, dataGatewayToken, ct);
    }

    public async Task<ResourceDto> RestoreFileVersionAsync(string id, int versionNumber, CancellationToken ct = default)
    {
        var existing = await LoadOrThrowAsync(id, ct);
        EnsureManagedOfficeFile(existing);

        var snapshot = await _perms.LoadSnapshotAsync(ct);
        snapshot.EnsureCan(existing, ResourceAction.Edit);

        var (bytes, fileName) = await ReadFileVersionBytesAsync(existing, versionNumber, null, ct);
        var newVersion = (existing.currentVersionNumber ?? 1) + 1;
        var profile = ResolveManagedOfficeProfile(existing);

        var filePayload = new Dictionary<string, object?>
        {
            ["content"] = Convert.ToBase64String(bytes),
            ["originalFileName"] = fileName
        };

        var payload = new Dictionary<string, object?>
        {
            ["file"] = filePayload,
            ["currentVersionNumber"] = newVersion,
            ["size"] = bytes.LongLength,
            ["mimeType"] = profile.MimeType,
            ["extension"] = profile.Extension
        };

        var updated = await _dg.UpdateAsync<DmResource>(DmDatasets.Resources, id, payload, Token, ct);
        await WriteFileVersionAsync(id, newVersion, bytes, fileName, $"restore from v{versionNumber}", ct);
        return ToDto(updated, snapshot.Resolve(updated));
    }

    public async Task<MarkdownVersionDto> UpdateFileVersionChangeNoteAsync(
        string id,
        int versionNumber,
        UpdateFileVersionChangeNoteRequest request,
        CancellationToken ct = default)
    {
        var existing = await LoadOrThrowAsync(id, ct);
        EnsureManagedOfficeFile(existing);

        var snapshot = await _perms.LoadSnapshotAsync(ct);
        snapshot.EnsureCan(existing, ResourceAction.Edit);

        var version = await LoadVersionOrThrowAsync(id, versionNumber, null, ct);
        if (string.IsNullOrWhiteSpace(version.__dataId))
            throw DocumentException.NotFound("Sürüm kaydı bulunamadı.");

        var note = ResolveChangeNote(request.ChangeNote, "save");
        await _dg.UpdateAsync<DmResourceVersion>(
            DmDatasets.ResourceVersions,
            version.__dataId,
            new Dictionary<string, object?> { ["changeNote"] = note },
            Token,
            ct);

        var currentVersion = existing.currentVersionNumber ?? 1;
        DmHistoryEntry? audit = null;
        if (version.createdAt is null && version.createdBy is null)
            BuildVersionAuditMap(existing.__history).TryGetValue(versionNumber, out audit);

        return new MarkdownVersionDto
        {
            VersionNumber = version.versionNumber ?? versionNumber,
            ChangeNote = note,
            Size = version.size,
            CreatedAt = version.createdAt ?? CreatedFrom(version.__history)?.timestamp ?? audit?.timestamp,
            CreatedBy = version.createdBy ?? CreatedFrom(version.__history)?.userEmail ?? audit?.userEmail,
            IsCurrent = versionNumber == currentVersion
        };
    }

    public async Task<int> SaveManagedDocumentFileAsync(
        string id,
        byte[] content,
        string fileName,
        string dataGatewayToken,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dataGatewayToken))
        {
            throw DocumentException.Validation(
                "AUTH_REQUIRED",
                "Bearer token is required.",
                "Oturum doğrulaması gerekli.");
        }

        var existing = await _dg.GetByIdAsync<DmResource>(DmDatasets.Resources, id, dataGatewayToken, ct);
        if (existing is null || existing.__dataId is null)
            throw DocumentException.NotFound("Dosya bulunamadı.");

        EnsureManagedOfficeFile(existing);

        var profile = ResolveManagedOfficeProfile(existing);
        var currentVersion = existing.currentVersionNumber ?? 1;
        var newVersion = currentVersion + 1;

        var filePayload = new Dictionary<string, object?>
        {
            ["content"] = Convert.ToBase64String(content),
            ["originalFileName"] = fileName
        };

        var payload = new Dictionary<string, object?>
        {
            ["file"] = filePayload,
            ["size"] = content.LongLength,
            ["mimeType"] = profile.MimeType,
            ["extension"] = profile.Extension,
            ["currentVersionNumber"] = newVersion
        };

        await _dg.UpdateAsync<DmResource>(DmDatasets.Resources, id, payload, dataGatewayToken, ct);
        await WriteFileVersionAsync(id, newVersion, content, fileName, "save", ct, dataGatewayToken);
        return newVersion;
    }

    public Task<ResourceDto> CreateNativeSheetAsync(CreateNativeOfficeRequest request, CancellationToken ct = default) =>
        CreateNativeOfficeAsync(request, ManagedOfficeKind.Sheet, ct);

    public Task<ResourceDto> CreateNativePresentationAsync(CreateNativeOfficeRequest request, CancellationToken ct = default) =>
        CreateNativeOfficeAsync(request, ManagedOfficeKind.Presentation, ct);

    private async Task<ResourceDto> CreateNativeOfficeAsync(
        CreateNativeOfficeRequest request,
        ManagedOfficeKind kind,
        CancellationToken ct)
    {
        ValidateName(request.Name);
        var profile = ManagedOfficeProfiles.Get(kind);
        var displayName = request.Name.Trim();
        var fileName = ManagedOfficeProfiles.EnsureFileNameHasExtension(displayName, profile);

        var documentNo = string.IsNullOrWhiteSpace(request.DocumentNo)
            ? DeriveDocumentNoFromName(displayName)
            : request.DocumentNo.Trim();
        await EnsureDocumentNoUniqueAsync(documentNo, ct);

        var officeBytes = ManagedOfficeEmptyFactory.CreateBlank(kind);

        return await CreateFileResourceCoreAsync(new CreateFileResourceRequest
        {
            ParentId = request.ParentId,
            Name = displayName,
            OriginalFileName = fileName,
            Description = request.Description,
            Tags = request.Tags,
            MimeType = profile.MimeType,
            Extension = "." + profile.Extension,
            Size = officeBytes.Length,
            Content = Convert.ToBase64String(officeBytes),
            Origin = ResourceOrigin.Native,
            DocumentNo = documentNo
        }, ct, ResourceAction.Create);
    }

    private static string DeriveDocumentNoFromName(string name)
    {
        var stem = Path.GetFileNameWithoutExtension(name.Trim());
        var sb = new System.Text.StringBuilder();
        var pendingDash = false;

        foreach (var c in stem)
        {
            if (char.IsLetterOrDigit(c))
            {
                if (pendingDash && sb.Length > 0)
                {
                    sb.Append('-');
                    pendingDash = false;
                }

                sb.Append(char.ToUpperInvariant(c));
            }
            else if (c is ' ' or '-' or '_' or '.')
            {
                pendingDash = sb.Length > 0;
            }
        }

        var code = sb.ToString().Trim('-');
        return string.IsNullOrEmpty(code)
            ? "DOC-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()
            : code;
    }

    public Task<ResourceDto> CreateFileResourceAsync(CreateFileResourceRequest request, CancellationToken ct = default) =>
        CreateFileResourceCoreAsync(request, ct, ResourceAction.Upload);

    private async Task<ResourceDto> CreateFileResourceCoreAsync(
        CreateFileResourceRequest request,
        CancellationToken ct,
        string parentAction,
        string? initialVersionChangeNote = null)
    {
        ValidateName(request.Name);
        if (string.IsNullOrWhiteSpace(request.Content))
            throw DocumentException.Validation("CONTENT_REQUIRED", "File content is required.", "Dosya içeriği zorunludur.");

        await EnsureCanOnParentAsync(request.ParentId, parentAction, ct);
        var ancestorIds = await ResolveAncestorsForChildAsync(request.ParentId, ct);

        // DG dm_resources.file (fieldType=file) alanı: { content (base64), originalFileName } verildiğinde
        // DataController dosyayı MinIO'ya yükler ve alanı { path, file_name, file_ext, file_size, ... } ile değiştirir.
        var filePayload = new Dictionary<string, object?>
        {
            ["content"] = request.Content,
            ["originalFileName"] = string.IsNullOrWhiteSpace(request.OriginalFileName) ? request.Name.Trim() : request.OriginalFileName
        };

        var tags = await ResolveTagsAsync(request.Tags, ct);

        var payload = new Dictionary<string, object?>
        {
            ["type"] = ResourceType.File,
            ["parentId"] = request.ParentId,
            ["ancestorIds"] = ancestorIds,
            ["name"] = request.Name.Trim(),
            ["title"] = request.Name.Trim(),
            ["description"] = request.Description,
            ["tags"] = tags,
            ["contentType"] = ResourceContentType.Binary,
            ["mimeType"] = request.MimeType,
            ["extension"] = request.Extension,
            ["size"] = request.Size,
            ["file"] = filePayload,
            ["currentVersionNumber"] = 1
        };

        if (!string.IsNullOrWhiteSpace(request.Origin))
            payload["origin"] = request.Origin.Trim();
        else
            payload["origin"] = ResourceOrigin.Upload;
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

        var origin = !string.IsNullOrWhiteSpace(request.Origin) ? request.Origin.Trim() : ResourceOrigin.Upload;
        if (ResourceOrigin.IsManagedDocument(origin))
        {
            var bytes = Convert.FromBase64String(request.Content);
            var fileName = string.IsNullOrWhiteSpace(request.OriginalFileName) ? request.Name.Trim() : request.OriginalFileName;
            if (!fileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
                fileName += ".docx";
            await WriteFileVersionAsync(
                created.__dataId!,
                1,
                bytes,
                fileName,
                initialVersionChangeNote ?? "initial",
                ct);
        }

        return await ToDtoWithEffectiveAsync(created, ct);
    }

    public async Task<ResourceDto> CloneResourceAsync(string id, CloneResourceRequest request, CancellationToken ct = default)
    {
        ValidateName(request.Name);
        var source = await LoadOrThrowAsync(id, ct);
        var snapshot = await _perms.LoadSnapshotAsync(ct);
        snapshot.EnsureCan(source, ResourceAction.View);

        if (IsCloneableMarkdown(source))
            return await CloneMarkdownResourceAsync(source, request, snapshot, ct);

        if (IsCloneableManagedDocx(source))
            return await CloneManagedDocxResourceAsync(source, request, snapshot, ct);

        throw DocumentException.Validation(
            "NOT_CLONEABLE",
            "Resource type cannot be cloned.",
            "Bu kaynak klonlanamaz.");
    }

    private async Task<ResourceDto> CloneMarkdownResourceAsync(
        DmResource source,
        CloneResourceRequest request,
        PermissionSnapshot snapshot,
        CancellationToken ct)
    {
        var content = source.content ?? string.Empty;
        ValidateContentLength(content);
        await EnsureCanOnParentAsync(request.ParentId, ResourceAction.Create, ct);

        var ancestorIds = await ResolveAncestorsForChildAsync(request.ParentId, ct);
        var title = request.Name.Trim();
        var isDraft = ResourceStatus.Normalize(source.status) == ResourceStatus.Draft;
        var sourceLabel = CloneSourceLabel(source);

        var payload = new Dictionary<string, object?>
        {
            ["type"] = ResourceType.Markdown,
            ["parentId"] = request.ParentId,
            ["ancestorIds"] = ancestorIds,
            ["name"] = title,
            ["title"] = title,
            ["tags"] = new List<string>(),
            ["content"] = content,
            ["contentType"] = ResourceContentType.Markdown,
            ["extension"] = "md",
            ["mimeType"] = "text/markdown",
            ["size"] = (long)content.Length,
            ["currentVersionNumber"] = 1,
            ["status"] = isDraft ? ResourceStatus.Draft : ResourceStatus.Published
        };

        var created = await _dg.CreateAsync<DmResource>(DmDatasets.Resources, payload, Token, ct);
        await WriteVersionAsync(created.__dataId!, 1, content, $"clone from {sourceLabel}", ct);
        return ToDto(created, snapshot.Resolve(created));
    }

    private async Task<ResourceDto> CloneManagedDocxResourceAsync(
        DmResource source,
        CloneResourceRequest request,
        PermissionSnapshot snapshot,
        CancellationToken ct)
    {
        ValidateDocumentNo(request.DocumentNo);
        var documentNo = request.DocumentNo!.Trim();
        await EnsureDocumentNoUniqueAsync(documentNo, ct);

        var effective = snapshot.Resolve(source);
        if (!effective.CanDownload)
            throw DocumentException.Forbidden("Klonlamak için indirme yetkisi gerekir.");

        var (path, storedName) = ReadFileField(source.file);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw DocumentException.Validation(
                "FILE_MISSING",
                "File content is missing.",
                "Dosya içeriği bulunamadı.");
        }

        var docxBytes = await _dg.DownloadFileAsync(path, Token, ct);
        var displayName = request.Name.Trim();
        var fileName = !string.IsNullOrWhiteSpace(storedName)
            ? storedName!
            : displayName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)
                ? displayName
                : $"{displayName}.docx";

        var sourceOrigin = string.IsNullOrWhiteSpace(source.origin)
            ? ResourceOrigin.Native
            : source.origin.Trim();
        var sourceLabel = CloneSourceLabel(source);
        return await CreateFileResourceCoreAsync(new CreateFileResourceRequest
        {
            ParentId = request.ParentId,
            Name = displayName,
            OriginalFileName = fileName,
            MimeType = DocxMime,
            Extension = ".docx",
            Size = docxBytes.LongLength,
            Content = Convert.ToBase64String(docxBytes),
            Origin = sourceOrigin,
            TemplateId = source.templateId,
            TemplateCode = source.templateCode,
            GenerationProfile = source.generationProfile,
            LetterheadId = source.letterheadId,
            DocumentNo = documentNo,
            Tags = new List<string>()
        }, ct, ResourceAction.Create, $"clone from {sourceLabel}");
    }

    private static bool IsCloneableMarkdown(DmResource resource) =>
        string.Equals(resource.type, ResourceType.Markdown, StringComparison.OrdinalIgnoreCase);

    private static bool IsCloneableManagedDocx(DmResource resource)
    {
        if (!string.Equals(resource.type, ResourceType.File, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!ResourceOrigin.IsManagedDocument(resource.origin))
            return false;

        var ext = (resource.extension ?? string.Empty).Trim().TrimStart('.');
        var mime = resource.mimeType ?? string.Empty;
        return string.Equals(ext, "docx", StringComparison.OrdinalIgnoreCase)
            || mime.Contains("wordprocessingml", StringComparison.OrdinalIgnoreCase);
    }

    private static string CloneSourceLabel(DmResource source)
    {
        if (IsCloneableMarkdown(source))
            return source.title ?? source.name ?? source.__dataId ?? "unknown";

        return source.documentNo ?? source.name ?? source.__dataId ?? "unknown";
    }

    public async Task<ResourceDto> CreateNativeDocumentAsync(CreateNativeDocumentRequest request, CancellationToken ct = default)
    {
        ValidateName(request.Name);
        ValidateDocumentNo(request.DocumentNo);
        var documentNo = request.DocumentNo.Trim();
        await EnsureDocumentNoUniqueAsync(documentNo, ct);

        var displayName = request.Name.Trim();
        var fileName = displayName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)
            ? displayName
            : $"{displayName}.docx";

        var docxBytes = MinimalDocxFactory.CreateBlank();
        string? letterheadId = null;

        if (!string.IsNullOrWhiteSpace(request.LetterheadId))
        {
            letterheadId = request.LetterheadId.Trim();
            await _letterheads.EnsureActiveAsync(letterheadId, ct);

            var letterheadEntry = await _letterheads.GetByIdAsync(letterheadId, ct);
            var capabilities = letterheadEntry.Settings.HeaderFields;
            var effectiveHeader = ResolveNativeHeaderFields(capabilities, request.SelectedHeaderFields);

            var letterheadResolve = await _letterheads.ResolveAsync(letterheadId, null, ct);
            byte[]? letterheadDesignDocx = null;
            if (letterheadEntry.HasDesign && !string.IsNullOrWhiteSpace(letterheadEntry.DesignStoragePath))
                letterheadDesignDocx = await _dg.DownloadFileAsync(letterheadEntry.DesignStoragePath, Token, ct);

            var templateModel = new TemplateModelDocument
            {
                PageLayout = TemplatePageLayoutModel.CreateDefault()
            };
            var baseLetterheadModel = letterheadResolve.Letterhead is { Enabled: true }
                ? TemplateModelSerializer.ToLetterheadModel(letterheadResolve.Letterhead)
                : null;
            var letterheadModel = baseLetterheadModel is not null
                ? LetterheadSettingsSerializer.ApplyHeaderFields(baseLetterheadModel, effectiveHeader)
                : null;

            var brandingSettings = CloneLetterheadSettingsWithHeaderFields(letterheadEntry.Settings, effectiveHeader);
            var (footerModel, pageLayout) = LetterheadBrandingResolver.Resolve(
                new LetterheadResolveResult
                {
                    Letterhead = letterheadResolve.Letterhead,
                    Settings = brandingSettings,
                    LetterheadId = letterheadResolve.LetterheadId,
                    LetterheadCode = letterheadResolve.LetterheadCode,
                    LetterheadName = letterheadResolve.LetterheadName,
                    Footer = letterheadResolve.Footer,
                    PageLayout = letterheadResolve.PageLayout
                },
                templateModel);

            docxBytes = await _brandingApplier.ApplyAsync(
                docxBytes,
                displayName,
                letterheadModel,
                footerModel,
                pageLayout,
                letterheadDesignDocx,
                brandingSettings,
                Token,
                ct);

            if (letterheadDesignDocx is { Length: > 0 }
                && LetterheadDesignMerger.HasBrokenHeaderImages(docxBytes))
            {
                docxBytes = LetterheadDesignMerger.EnsureHeaderWithMediaFromDesign(docxBytes, letterheadDesignDocx);
            }

            var enrichLetterhead = CloneLetterheadDtoWithSettings(letterheadEntry, brandingSettings);
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            await _headerEnricher.EnrichAsync(
                values,
                templateModel,
                enrichLetterhead,
                displayName,
                _ctx,
                allocateCounters: true,
                Token,
                ct);

            if (effectiveHeader.DocumentName)
                values[LetterheadConstants.DocumentNameKey] = displayName;

            ApplyUnselectedHeaderClears(values, capabilities, effectiveHeader);
            docxBytes = DocxPlaceholderMerger.Merge(docxBytes, values);
        }

        return await CreateFileResourceCoreAsync(new CreateFileResourceRequest
        {
            ParentId = request.ParentId,
            Name = displayName,
            OriginalFileName = fileName,
            Description = request.Description,
            Tags = request.Tags,
            MimeType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            Extension = ".docx",
            Size = docxBytes.Length,
            Content = Convert.ToBase64String(docxBytes),
            Origin = ResourceOrigin.Native,
            LetterheadId = letterheadId,
            DocumentNo = documentNo
        }, ct, ResourceAction.Create);
    }

    private static LetterheadHeaderFieldsDto ResolveNativeHeaderFields(
        LetterheadHeaderFieldsDto capabilities,
        LetterheadHeaderFieldsDto? userSelection)
    {
        var selection = userSelection ?? capabilities;
        return new LetterheadHeaderFieldsDto
        {
            DocumentName = capabilities.DocumentName && selection.DocumentName,
            DocNo = capabilities.DocNo && selection.DocNo,
            GeneratedAt = capabilities.GeneratedAt && selection.GeneratedAt,
            CreatePerson = capabilities.CreatePerson && selection.CreatePerson
        };
    }

    private static LetterheadSettingsDto CloneLetterheadSettingsWithHeaderFields(
        LetterheadSettingsDto source,
        LetterheadHeaderFieldsDto headerFields) =>
        new()
        {
            HeaderFields = headerFields,
            GeneralDocNo = source.GeneralDocNo,
            Footer = source.Footer,
            LegacyOdakFooter = source.LegacyOdakFooter,
            FooterBlocks = source.FooterBlocks,
            PageLayout = source.PageLayout
        };

    private static LetterheadDto CloneLetterheadDtoWithSettings(LetterheadDto source, LetterheadSettingsDto settings) =>
        new()
        {
            Id = source.Id,
            Name = source.Name,
            Code = source.Code,
            Description = source.Description,
            IsDefault = source.IsDefault,
            IsActive = source.IsActive,
            Letterhead = source.Letterhead,
            Settings = settings,
            DesignStoragePath = source.DesignStoragePath,
            DesignFileName = source.DesignFileName,
            HasDesign = source.HasDesign,
            CreatedBy = source.CreatedBy,
            CreatedAt = source.CreatedAt,
            UpdatedAt = source.UpdatedAt
        };

    private static void ApplyUnselectedHeaderClears(
        Dictionary<string, string> values,
        LetterheadHeaderFieldsDto capabilities,
        LetterheadHeaderFieldsDto effective)
    {
        if (capabilities.DocumentName && !effective.DocumentName)
            values[LetterheadConstants.DocumentNameKey] = string.Empty;
        if (capabilities.DocNo && !effective.DocNo)
            values[LetterheadConstants.DocNoKey] = string.Empty;
        if (capabilities.GeneratedAt && !effective.GeneratedAt)
            values[LetterheadConstants.GeneratedAtKey] = string.Empty;
        if (capabilities.CreatePerson && !effective.CreatePerson)
            values[LetterheadConstants.CreatePersonKey] = string.Empty;
    }

    public async Task<(byte[] PdfBytes, string FileName)> GetFilePreviewPdfAsync(string id, CancellationToken ct = default)
    {
        var resource = await LoadOrThrowAsync(id, ct);
        if (!string.Equals(resource.type, ResourceType.File, StringComparison.OrdinalIgnoreCase))
        {
            throw DocumentException.Validation(
                "NOT_FILE",
                "Resource is not a file.",
                "Kaynak bir dosya değil.");
        }

        var origin = string.IsNullOrWhiteSpace(resource.origin) ? ResourceOrigin.Upload : resource.origin.Trim();
        if (ResourceOrigin.IsManagedDocument(origin))
        {
            throw DocumentException.Validation(
                "PREVIEW_NOT_AVAILABLE",
                "Managed documents are not previewed via PDF conversion.",
                "Bu döküman PDF önizleme ile görüntülenemez.");
        }

        var ext = (resource.extension ?? string.Empty).Trim().TrimStart('.');
        var mime = resource.mimeType ?? string.Empty;
        var isDocx = string.Equals(ext, "docx", StringComparison.OrdinalIgnoreCase)
                     || mime.Contains("wordprocessingml", StringComparison.OrdinalIgnoreCase);
        if (!isDocx)
        {
            throw DocumentException.Validation(
                "UNSUPPORTED_PREVIEW",
                "Only DOCX files can be previewed as PDF.",
                "Yalnızca DOCX dosyaları PDF olarak önizlenebilir.");
        }

        var snapshot = await _perms.LoadSnapshotAsync(ct);
        snapshot.EnsureCan(resource, ResourceAction.View);
        var effective = snapshot.Resolve(resource);
        if (!effective.CanDownload)
            throw DocumentException.Forbidden("Önizleme için indirme yetkisi gerekir.");

        var (path, storedName) = ReadFileField(resource.file);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw DocumentException.Validation(
                "FILE_MISSING",
                "File content is missing.",
                "Dosya içeriği bulunamadı.");
        }

        var docxBytes = await _dg.DownloadFileAsync(path, Token, ct);
        byte[] pdfBytes;
        try
        {
            pdfBytes = await _render.ConvertDocxToPdfAsync(docxBytes, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DOCX to PDF preview conversion failed for resource {ResourceId}", id);
            throw DocumentException.ServiceUnavailable(
                "PREVIEW_CONVERSION_FAILED",
                "Document could not be converted to PDF.",
                "Dosya PDF olarak önizlenemedi. İndirmeyi deneyin.");
        }

        var baseName = !string.IsNullOrWhiteSpace(storedName) ? storedName! : resource.name ?? "document.docx";
        var pdfName = Path.GetFileNameWithoutExtension(baseName) + ".pdf";
        return (pdfBytes, pdfName);
    }

    public async Task<(byte[] PdfBytes, string FileName)> GetFileExportPdfAsync(string id, CancellationToken ct = default)
    {
        var resource = await LoadOrThrowAsync(id, ct);
        if (!string.Equals(resource.type, ResourceType.File, StringComparison.OrdinalIgnoreCase))
        {
            throw DocumentException.Validation(
                "NOT_FILE",
                "Resource is not a file.",
                "Kaynak bir dosya değil.");
        }

        var ext = (resource.extension ?? string.Empty).Trim().TrimStart('.');
        var mime = resource.mimeType ?? string.Empty;
        if (!ManagedOfficeProfiles.TryResolve(ext, mime, out var profile))
        {
            throw DocumentException.Validation(
                "UNSUPPORTED_EXPORT",
                "Only DOCX, XLSX and PPTX files can be exported as PDF.",
                "Yalnızca DOCX, XLSX ve PPTX dosyaları PDF olarak dışa aktarılabilir.");
        }

        if (profile.Kind is ManagedOfficeKind.Sheet or ManagedOfficeKind.Presentation
            && !ResourceOrigin.IsManagedDocument(resource.origin))
        {
            throw DocumentException.Validation(
                "UNSUPPORTED_EXPORT",
                "Only managed sheets and presentations can be exported as PDF.",
                "Yalnızca yönetilen sheet ve sunumlar PDF olarak dışa aktarılabilir.");
        }

        var snapshot = await _perms.LoadSnapshotAsync(ct);
        snapshot.EnsureCan(resource, ResourceAction.View);
        var effective = snapshot.Resolve(resource);
        if (!effective.CanDownload)
            throw DocumentException.Forbidden("PDF indirmek için indirme yetkisi gerekir.");

        var (path, storedName) = ReadFileField(resource.file);
        if (string.IsNullOrWhiteSpace(path))
        {
            throw DocumentException.Validation(
                "FILE_MISSING",
                "File content is missing.",
                "Dosya içeriği bulunamadı.");
        }

        var fileBytes = await _dg.DownloadFileAsync(path, Token, ct);
        var sourceName = !string.IsNullOrWhiteSpace(storedName)
            ? storedName!
            : ManagedOfficeProfiles.EnsureFileNameHasExtension(resource.name ?? resource.title, profile);
        byte[] pdfBytes;
        try
        {
            pdfBytes = await _render.ConvertOfficeFileToPdfAsync(fileBytes, sourceName, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Office to PDF export conversion failed for resource {ResourceId}", id);
            throw DocumentException.ServiceUnavailable(
                "EXPORT_CONVERSION_FAILED",
                "Document could not be converted to PDF.",
                "Dosya PDF olarak dışa aktarılamadı.");
        }

        var pdfName = Path.GetFileNameWithoutExtension(sourceName) + ".pdf";
        return (pdfBytes, pdfName);
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

    private Task<IReadOnlyList<TreeNodeDto>> QueryLazyTreeFolderNodesAsync(string? parentId, CancellationToken ct) =>
        QueryLazyTreeFolderNodesAsync(parentId, null, ct);

    private async Task<IReadOnlyList<TreeNodeDto>> QueryLazyTreeFolderNodesAsync(
        string? parentId,
        PermissionSnapshot? snapshot,
        CancellationToken ct)
    {
        snapshot ??= await _perms.LoadSnapshotAsync(ct);

        var match = new Dictionary<string, object?>
        {
            ["type"] = ResourceType.Folder,
            ["parentId"] = parentId
        };
        var page = await _dg.QueryPageAsync(DmDatasets.Resources, match, TreeFolderListQuery, Token, ct);

        var folders = page.Items
            .Select(MapRow)
            .Where(f => f.__dataId is not null && snapshot.Resolve(f).CanView)
            .OrderBy(f => f.name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var hasChildren = await ResolveFolderHasChildrenAsync(
            folders.Select(f => f.__dataId!).ToList(),
            snapshot,
            ct);

        return folders
            .Select(f => new TreeNodeDto
            {
                Id = f.__dataId!,
                Name = f.name ?? string.Empty,
                ParentId = f.parentId,
                HasChildren = hasChildren.Contains(f.__dataId!),
                Children = new List<TreeNodeDto>()
            })
            .ToList();
    }

    private async Task<HashSet<string>> ResolveFolderHasChildrenAsync(
        IReadOnlyList<string> folderIds,
        PermissionSnapshot snapshot,
        CancellationToken ct)
    {
        if (folderIds.Count == 0)
            return new HashSet<string>(StringComparer.Ordinal);

        var match = new Dictionary<string, object?>
        {
            ["type"] = ResourceType.Folder,
            ["parentId"] = new Dictionary<string, object?> { ["$in"] = folderIds }
        };
        var page = await _dg.QueryPageAsync(DmDatasets.Resources, match, TreeFolderListQuery, Token, ct);

        var parentsWithVisibleChild = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in page.Items)
        {
            var child = MapRow(row);
            if (string.IsNullOrWhiteSpace(child.parentId))
                continue;
            if (!snapshot.Resolve(child).CanView)
                continue;
            parentsWithVisibleChild.Add(child.parentId);
        }

        return parentsWithVisibleChild;
    }

    private async Task<ResourceListResult> QueryChildrenAsync(
        string? parentId,
        PermissionSnapshot snapshot,
        CancellationToken ct,
        int skip = 0,
        int? limit = null)
    {
        var all = await GetVisibleChildrenCachedAsync(parentId, snapshot, ct);
        var total = all.Count;

        if (limit is null or <= 0)
            return new ResourceListResult { Items = all, Total = total };

        var safeLimit = Math.Clamp(limit.Value, 1, _settings.Resources.MaxChildrenPageSize);
        var safeSkip = Math.Max(0, skip);
        var pageItems = all.Skip(safeSkip).Take(safeLimit).ToList();
        return new ResourceListResult { Items = pageItems, Total = total };
    }

    private async Task<List<ResourceDto>> GetVisibleChildrenCachedAsync(
        string? parentId,
        PermissionSnapshot snapshot,
        CancellationToken ct)
    {
        var cacheKey = parentId ?? "__root__";
        if (_visibleChildrenCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var match = new Dictionary<string, object?> { ["parentId"] = parentId };
        var rows = new List<DmResource>();
        var dgSkip = 0;
        const int dgPageSize = 500;

        while (true)
        {
            var query = $"limit={dgPageSize}&skip={dgSkip}&{ChildrenListQuerySuffix}";
            var page = await _dg.QueryPageAsync(DmDatasets.Resources, match, query, Token, ct);
            if (page.Items.Count == 0)
                break;

            rows.AddRange(page.Items.Select(MapRow));
            if (page.Items.Count < dgPageSize)
                break;

            dgSkip += dgPageSize;
        }

        var visible = rows
            .Where(r => snapshot.Resolve(r).CanView)
            .OrderByDescending(r => r.type == ResourceType.Folder)
            .ThenBy(r => r.name, StringComparer.OrdinalIgnoreCase)
            .Select(r => ToDto(r, snapshot.Resolve(r)))
            .ToList();

        _visibleChildrenCache[cacheKey] = visible;
        return visible;
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

    private async Task WriteFileVersionAsync(
        string resourceId,
        int versionNumber,
        byte[] content,
        string fileName,
        string changeNote,
        CancellationToken ct,
        string? dataGatewayToken = null)
    {
        var token = dataGatewayToken ?? Token;
        var payload = new Dictionary<string, object?>
        {
            ["resourceId"] = resourceId,
            ["versionNumber"] = versionNumber,
            ["changeNote"] = changeNote,
            ["contentSnapshot"] = Convert.ToBase64String(content),
            ["filePathSnapshot"] = fileName,
            ["size"] = content.LongLength,
            ["mimeType"] = DocxMime,
            ["createdBy"] = _ctx.Username,
            ["createdAt"] = DateTime.UtcNow
        };

        try
        {
            await _dg.CreateAsync<Dictionary<string, object?>>(DmDatasets.ResourceVersions, payload, token, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write file version {Version} for resource {ResourceId}", versionNumber, resourceId);
        }
    }

    private async Task<IReadOnlyList<MarkdownVersionDto>> ListVersionDtosAsync(DmResource resource, CancellationToken ct)
    {
        var currentVersion = resource.currentVersionNumber ?? 1;
        var auditMap = BuildVersionAuditMap(resource.__history);
        var match = new Dictionary<string, object?> { ["resourceId"] = resource.__dataId! };
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

    private async Task<(byte[] Content, string FileName)> ReadFileVersionBytesAsync(
        DmResource resource,
        int versionNumber,
        string? dataGatewayToken,
        CancellationToken ct)
    {
        var version = await LoadVersionOrThrowAsync(resource.__dataId!, versionNumber, dataGatewayToken, ct);
        var base64 = version.contentSnapshot;
        if (string.IsNullOrWhiteSpace(base64))
        {
            throw DocumentException.Validation(
                "VERSION_CONTENT_MISSING",
                "Version file snapshot is missing.",
                "Sürüm dosya içeriği bulunamadı.");
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            throw DocumentException.Validation(
                "VERSION_CONTENT_INVALID",
                "Version file snapshot is invalid.",
                "Sürüm dosya içeriği geçersiz.");
        }

        var fileName = version.filePathSnapshot;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            var (_, storedName) = ReadFileField(resource.file);
            var profile = ResolveManagedOfficeProfile(resource);
            fileName = storedName ?? resource.name ?? resource.title ?? profile.DefaultFileName;
        }

        var resolvedProfile = ResolveManagedOfficeProfile(resource);
        fileName = ManagedOfficeProfiles.EnsureFileNameHasExtension(fileName, resolvedProfile);

        return (bytes, fileName);
    }

    private static void EnsureManagedOfficeFile(DmResource resource)
    {
        if (!string.Equals(resource.type, ResourceType.File, StringComparison.OrdinalIgnoreCase))
        {
            throw DocumentException.Validation(
                "NOT_FILE",
                "Resource is not a file.",
                "Kaynak bir dosya değil.");
        }

        if (!ResourceOrigin.IsManagedDocument(resource.origin))
        {
            throw DocumentException.Validation(
                "NOT_MANAGED_DOCUMENT",
                "Resource is not a managed document.",
                "Kaynak yönetilen bir döküman değil.");
        }

        if (!ManagedOfficeProfiles.TryResolve(resource.extension, resource.mimeType, out _))
        {
            throw DocumentException.Validation(
                "UNSUPPORTED_FILE_TYPE",
                "Only DOCX, XLSX and PPTX managed documents support version history.",
                "Sürüm geçmişi yalnızca DOCX, XLSX ve PPTX dökümanlar için desteklenir.");
        }
    }

    private static ManagedOfficeProfile ResolveManagedOfficeProfile(DmResource resource)
    {
        if (!ManagedOfficeProfiles.TryResolve(resource.extension, resource.mimeType, out var profile))
        {
            throw DocumentException.Validation(
                "UNSUPPORTED_FILE_TYPE",
                "Unsupported managed office file type.",
                "Desteklenmeyen Office dosya türü.");
        }

        return profile;
    }

    private static List<TreeNodeDto> BuildTree(IReadOnlyList<DmResource> folders)
    {
        var nodes = folders
            .Where(f => f.__dataId is not null)
            .ToDictionary(
                f => f.__dataId!,
                f => new TreeNodeDto
                {
                    Id = f.__dataId!,
                    Name = f.name ?? string.Empty,
                    ParentId = f.parentId,
                    HasChildren = false,
                    Children = new List<TreeNodeDto>()
                });

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

        foreach (var node in nodes.Values)
            node.HasChildren = node.Children.Count > 0;

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

    private async Task<DmResourceVersion> LoadVersionOrThrowAsync(
        string id,
        int versionNumber,
        string? dataGatewayToken,
        CancellationToken ct)
    {
        var token = dataGatewayToken ?? Token;
        var match = new Dictionary<string, object?>
        {
            ["resourceId"] = id,
            ["versionNumber"] = versionNumber
        };
        var page = await _dg.QueryPageAsync(DmDatasets.ResourceVersions, match, ListQuery, token, ct);
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

    private async Task EnsureDocumentNoUniqueAsync(string documentNo, CancellationToken ct)
    {
        var match = new Dictionary<string, object?> { ["documentNo"] = documentNo.Trim() };
        var page = await _dg.QueryPageAsync(
            DmDatasets.Resources,
            match,
            "limit=1&expand=false&showHistory=false",
            Token,
            ct);
        if (page.Items.Count > 0)
        {
            throw DocumentException.Conflict(
                "DOCUMENT_NO_EXISTS",
                "Document number already exists.",
                "Döküman kodu zaten kullanılıyor.");
        }
    }

    private async Task<List<string>> ResolveTagsAsync(IReadOnlyList<string>? tags, CancellationToken ct)
    {
        var normalized = await _tags.NormalizeActiveTagNamesAsync(tags, ct);
        return normalized.ToList();
    }

    private static void ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw DocumentException.Validation("NAME_REQUIRED", "Name is required.", "İsim zorunludur.");
    }

    private static void ValidateDocumentNo(string? documentNo)
    {
        if (string.IsNullOrWhiteSpace(documentNo))
        {
            throw DocumentException.Validation(
                "DOCUMENT_NO_REQUIRED",
                "Document code is required.",
                "Döküman kodu zorunludur.");
        }
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
