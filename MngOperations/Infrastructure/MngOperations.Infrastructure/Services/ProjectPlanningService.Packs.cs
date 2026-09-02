using MngOperations.Application.Contracts.Planning;
using MngOperations.Application.Exceptions;
using MngOperations.Application.Models;
using MngOperations.Application.Packs;
using MngOperations.Domain.Constants;

namespace MngOperations.Infrastructure.Services;

public sealed partial class ProjectPlanningService
{
    private sealed class PackApplyCounts
    {
        public int Created { get; set; }
        public int Skipped { get; set; }
        public int Updated { get; set; }
        public int Removed { get; set; }
        public int Kept { get; set; }
    }

    public async Task<ProjectPackCatalogDto> GetProjectPacksAsync(string projectId, CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(projectId, token, ct);
        var installed = await LoadProjectPackRowsAsync(projectId, token, ct);
        return new ProjectPackCatalogDto
        {
            Catalog = JobPackCatalog.All.Select(JobPackCatalog.ToDto).ToList(),
            Installed = installed.Select(ToInstallDto).ToList()
        };
    }

    public async Task<PackPreviewDto> PreviewPackAsync(
        string projectId,
        string packCode,
        string? intent = null,
        string? mode = null,
        CancellationToken ct = default)
    {
        var token = RequireToken();
        var project = await LoadProjectOrThrowAsync(projectId, token, ct);
        var pack = RequirePack(packCode);
        var intentNorm = NormalizePackIntent(intent);
        var modeNorm = NormalizePackMode(mode);
        var installed = await FindProjectPackRowAsync(projectId, pack.Code, token, ct);
        var installedVersion = installed is null ? null : JobPackCatalog.NormalizeVersion(installed.version);
        var catalogVersion = JobPackCatalog.NormalizeVersion(pack.Version);
        var existing = await LoadWbsAsync(projectId, token, ct);
        var items = intentNorm == "detach"
            ? BuildDetachPreview(pack, existing)
            : BuildApplyPreview(pack, existing, applyUpdates: modeNorm == "update");
        var workspace = intentNorm == "detach"
            ? new PackWorkspaceEnsureResult
            {
                Created = false,
                WorkspaceId = project.workspaceId,
                Action = "skip",
                WorkspaceName = PackWorkspaceName(project.code)
            }
            : await PreviewPackWorkspaceAsync(project, token, ct);

        return new PackPreviewDto
        {
            PackCode = pack.Code,
            Name = pack.Name,
            Version = catalogVersion,
            InstalledVersion = installedVersion,
            Outdated = installedVersion is not null
                && !string.Equals(installedVersion, catalogVersion, StringComparison.OrdinalIgnoreCase),
            Intent = intentNorm,
            CreateCount = items.Count(i => i.Action == "create"),
            SkipCount = items.Count(i => i.Action == "skip"),
            UpdateCount = items.Count(i => i.Action == "update"),
            RemoveCount = items.Count(i => i.Action == "remove"),
            KeepCount = items.Count(i => i.Action == "keep"),
            Items = items,
            WorkspaceAction = workspace.Action,
            WorkspaceId = workspace.WorkspaceId,
            WorkspaceName = workspace.WorkspaceName
        };
    }

    public async Task<ApplyPackResultDto> ApplyPackAsync(
        string projectId,
        string packCode,
        string? mode = null,
        CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(projectId, token, ct);
        var pack = RequirePack(packCode);
        var applyUpdates = NormalizePackMode(mode) == "update";
        var counts = await ApplyPackWbsAsync(projectId, pack, token, ct, applyUpdates);
        var workspace = await EnsurePackWorkspaceAsync(projectId, pack, token, ct);
        await UpsertProjectPackAsync(projectId, pack, token, ct);
        return new ApplyPackResultDto
        {
            PackCode = pack.Code,
            Version = JobPackCatalog.NormalizeVersion(pack.Version),
            Created = counts.Created,
            Skipped = counts.Skipped,
            Updated = counts.Updated,
            WorkspaceCreated = workspace.Created,
            WorkspaceId = workspace.WorkspaceId
        };
    }

    public async Task<ApplyPackResultDto> DetachPackAsync(string projectId, string packCode, CancellationToken ct = default)
    {
        var token = RequireToken();
        await LoadProjectOrThrowAsync(projectId, token, ct);
        var pack = RequirePack(packCode);
        var existing = await FindProjectPackRowAsync(projectId, pack.Code, token, ct);
        var counts = await RemovePackWbsAsync(projectId, pack, token, ct);
        if (existing is not null && !string.IsNullOrWhiteSpace(existing.__dataId))
            await _dg.DeleteAsync(PmDatasets.ProjectPacks, existing.__dataId, token, ct);

        await RecalcWbsCodesAsync(projectId, token, ct);
        await RecalcProjectProgressAsync(projectId, token, ct);

        return new ApplyPackResultDto
        {
            PackCode = pack.Code,
            Version = JobPackCatalog.NormalizeVersion(pack.Version),
            Removed = counts.Removed,
            Kept = counts.Kept
        };
    }

    private static JobPackDefinition RequirePack(string packCode) =>
        JobPackCatalog.Find(packCode)
        ?? throw new OperationCoreException("PACK_UNKNOWN", "Unknown job pack.", "Bilinmeyen iş paketi.", 400);

    private static string NormalizePackIntent(string? intent)
    {
        var value = (intent ?? "apply").Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(value) || value == "apply") return "apply";
        if (value == "detach") return "detach";
        throw new OperationCoreException("PACK_INTENT", "Pack preview intent must be apply or detach.", "Önizleme amacı apply veya detach olmalı.", 400);
    }

    private static string NormalizePackMode(string? mode)
    {
        var value = (mode ?? "skip").Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(value) || value == "skip") return "skip";
        if (value == "update") return "update";
        throw new OperationCoreException("PACK_MODE", "Pack apply mode must be skip or update.", "Paket kurulum kipi skip veya update olmalı.", 400);
    }

    private static List<PackPreviewItemDto> BuildApplyPreview(
        JobPackDefinition pack,
        List<PmWbsRow> existing,
        bool applyUpdates)
    {
        var items = new List<PackPreviewItemDto>();
        WalkApplyPreview(null, pack.Wbs, string.Empty, existing, applyUpdates, items);
        return items;
    }

    private static void WalkApplyPreview(
        string? parentId,
        IReadOnlyList<JobPackWbsNode> nodes,
        string pathPrefix,
        List<PmWbsRow> existing,
        bool applyUpdates,
        List<PackPreviewItemDto> items)
    {
        foreach (var node in nodes)
        {
            var name = (node.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new OperationCoreException("PACK_WBS", "Pack WBS name is required.", "Paket WBS adı zorunludur.", 400);

            var path = string.IsNullOrEmpty(pathPrefix) ? name : $"{pathPrefix} / {name}";
            var kind = PmWbsKind.Normalize(node.Kind);
            var match = FindWbsByName(existing, parentId, name);
            string action;
            string? childParentId;
            if (match is null || string.IsNullOrWhiteSpace(match.__dataId))
            {
                action = "create";
                childParentId = "\0pack-preview";
            }
            else if (applyUpdates && CanUpdateWbs(match) && PackNodeDiffers(node, match))
            {
                action = "update";
                childParentId = match.__dataId;
            }
            else
            {
                action = "skip";
                childParentId = match.__dataId;
            }

            items.Add(new PackPreviewItemDto
            {
                Path = path,
                Kind = kind,
                Action = action,
                WbsId = match?.__dataId
            });

            if (node.Children is { Count: > 0 })
                WalkApplyPreview(childParentId, node.Children, path, existing, applyUpdates, items);
        }
    }

    private static List<PackPreviewItemDto> BuildDetachPreview(JobPackDefinition pack, List<PmWbsRow> existing)
    {
        var remaining = existing.ToList();
        var removeIds = new HashSet<string>(StringComparer.Ordinal);
        CollectDetachRemovals(null, pack.Wbs, remaining, removeIds);

        var items = new List<PackPreviewItemDto>();
        WalkDetachPreview(null, pack.Wbs, string.Empty, existing, removeIds, items);
        return items;
    }

    private static void CollectDetachRemovals(
        string? parentId,
        IReadOnlyList<JobPackWbsNode> nodes,
        List<PmWbsRow> remaining,
        HashSet<string> removeIds)
    {
        foreach (var node in nodes)
        {
            var name = (node.Name ?? string.Empty).Trim();
            var match = FindWbsByName(remaining, parentId, name);
            if (node.Children is { Count: > 0 })
                CollectDetachRemovals(match?.__dataId, node.Children, remaining, removeIds);

            if (match is null || string.IsNullOrWhiteSpace(match.__dataId))
                continue;
            if (!CanDetachWbs(match, remaining))
                continue;

            removeIds.Add(match.__dataId);
            remaining.RemoveAll(w => string.Equals(w.__dataId, match.__dataId, StringComparison.Ordinal));
        }
    }

    private static void WalkDetachPreview(
        string? parentId,
        IReadOnlyList<JobPackWbsNode> nodes,
        string pathPrefix,
        List<PmWbsRow> existing,
        HashSet<string> removeIds,
        List<PackPreviewItemDto> items)
    {
        foreach (var node in nodes)
        {
            var name = (node.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name)) continue;
            var path = string.IsNullOrEmpty(pathPrefix) ? name : $"{pathPrefix} / {name}";
            var kind = PmWbsKind.Normalize(node.Kind);
            var match = FindWbsByName(existing, parentId, name);
            if (match is not null && !string.IsNullOrWhiteSpace(match.__dataId))
            {
                items.Add(new PackPreviewItemDto
                {
                    Path = path,
                    Kind = string.IsNullOrWhiteSpace(match.kind) ? kind : PmWbsKind.Normalize(match.kind),
                    Action = removeIds.Contains(match.__dataId) ? "remove" : "keep",
                    WbsId = match.__dataId
                });
            }

            if (node.Children is { Count: > 0 })
                WalkDetachPreview(match?.__dataId, node.Children, path, existing, removeIds, items);
        }
    }

    private async Task<PackApplyCounts> ApplyPackWbsAsync(
        string projectId,
        JobPackDefinition pack,
        string token,
        CancellationToken ct,
        bool applyUpdates = false)
    {
        var counts = new PackApplyCounts();
        if (pack.Wbs.Count == 0) return counts;
        var existing = await LoadWbsAsync(projectId, token, ct);
        await CreatePackWbsNodesAsync(projectId, null, pack.Wbs, existing, token, ct, counts, applyUpdates);
        await RecalcWbsCodesAsync(projectId, token, ct);
        await RecalcProjectProgressAsync(projectId, token, ct);
        return counts;
    }

    private async Task CreatePackWbsNodesAsync(
        string projectId,
        string? parentId,
        IReadOnlyList<JobPackWbsNode> nodes,
        List<PmWbsRow> existing,
        string token,
        CancellationToken ct,
        PackApplyCounts counts,
        bool applyUpdates)
    {
        var sort = NextSort(existing, parentId);
        foreach (var node in nodes)
        {
            var name = (node.Name ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                throw new OperationCoreException("PACK_WBS", "Pack WBS name is required.", "Paket WBS adı zorunludur.", 400);

            var match = FindWbsByName(existing, parentId, name);
            string id;
            if (match is not null && !string.IsNullOrWhiteSpace(match.__dataId))
            {
                var kind = PmWbsKind.Normalize(node.Kind);
                if (applyUpdates && CanUpdateWbs(match) && PackNodeDiffers(node, match))
                {
                    var weight = node.Weight ?? 1;
                    await _dg.UpdateAsync(PmDatasets.WbsItems, match.__dataId, new Dictionary<string, object?>
                    {
                        ["kind"] = kind,
                        ["weight"] = weight
                    }, token, ct);
                    match.kind = kind;
                    match.weight = weight;
                    counts.Updated++;
                }
                else
                {
                    counts.Skipped++;
                }

                id = match.__dataId;
            }
            else
            {
                var kind = PmWbsKind.Normalize(node.Kind);
                var payload = new Dictionary<string, object?>
                {
                    ["projectId"] = projectId,
                    ["parentId"] = parentId,
                    ["kind"] = kind,
                    ["name"] = name,
                    ["sortOrder"] = sort,
                    ["weight"] = node.Weight ?? 1,
                    ["percentComplete"] = 0
                };
                sort += 10;
                var created = await _dg.CreateAsync(PmDatasets.WbsItems, payload, token, ct);
                id = ReadId(created);
                if (string.IsNullOrWhiteSpace(id))
                    throw new OperationCoreException("CREATE_FAILED", "WBS create did not return an id.", "WBS oluşturulamadı.", 500);
                existing.Add(new PmWbsRow
                {
                    __dataId = id,
                    projectId = projectId,
                    parentId = parentId,
                    kind = kind,
                    name = name,
                    sortOrder = sort - 10,
                    weight = node.Weight ?? 1,
                    percentComplete = 0
                });
                counts.Created++;
            }

            if (node.Children is { Count: > 0 })
                await CreatePackWbsNodesAsync(projectId, id, node.Children, existing, token, ct, counts, applyUpdates);
        }
    }

    private async Task<PackApplyCounts> RemovePackWbsAsync(
        string projectId,
        JobPackDefinition pack,
        string token,
        CancellationToken ct)
    {
        var counts = new PackApplyCounts();
        var all = await LoadWbsAsync(projectId, token, ct);
        var deps = await LoadDepsAsync(projectId, token, ct);
        await RemovePackWbsNodesAsync(null, pack.Wbs, all, deps, token, ct, counts);
        return counts;
    }

    private async Task RemovePackWbsNodesAsync(
        string? parentId,
        IReadOnlyList<JobPackWbsNode> nodes,
        List<PmWbsRow> all,
        List<PmDependencyRow> deps,
        string token,
        CancellationToken ct,
        PackApplyCounts counts)
    {
        foreach (var node in nodes)
        {
            var name = (node.Name ?? string.Empty).Trim();
            var match = FindWbsByName(all, parentId, name);
            if (node.Children is { Count: > 0 })
                await RemovePackWbsNodesAsync(match?.__dataId, node.Children, all, deps, token, ct, counts);

            if (match is null || string.IsNullOrWhiteSpace(match.__dataId))
                continue;

            if (!CanDetachWbs(match, all))
            {
                counts.Kept++;
                continue;
            }

            var id = match.__dataId;
            foreach (var dep in deps.Where(d =>
                         string.Equals(d.predecessorId, id, StringComparison.Ordinal)
                         || string.Equals(d.successorId, id, StringComparison.Ordinal)).ToList())
            {
                if (!string.IsNullOrWhiteSpace(dep.__dataId))
                    await _dg.DeleteAsync(PmDatasets.Dependencies, dep.__dataId, token, ct);
                deps.Remove(dep);
            }

            await _dg.DeleteAsync(PmDatasets.WbsItems, id, token, ct);
            all.RemoveAll(w => string.Equals(w.__dataId, id, StringComparison.Ordinal));
            counts.Removed++;
        }
    }

    private static bool CanDetachWbs(PmWbsRow row, List<PmWbsRow> all)
    {
        if (!CanUpdateWbs(row)) return false;
        if (all.Any(w => string.Equals(w.parentId, row.__dataId, StringComparison.Ordinal))) return false;
        return true;
    }

    private static bool CanUpdateWbs(PmWbsRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.workItemId)) return false;
        if ((row.percentComplete ?? 0) >= 0.5) return false;
        return true;
    }

    private static bool PackNodeDiffers(JobPackWbsNode node, PmWbsRow row)
    {
        var kind = PmWbsKind.Normalize(node.Kind);
        var rowKind = PmWbsKind.Normalize(row.kind);
        if (!string.Equals(kind, rowKind, StringComparison.Ordinal)) return true;
        var weight = node.Weight ?? 1;
        var rowWeight = row.weight ?? 1;
        return Math.Abs(weight - rowWeight) > 0.0001;
    }

    private static PmWbsRow? FindWbsByName(List<PmWbsRow> items, string? parentId, string name) =>
        items.FirstOrDefault(w =>
            string.Equals(w.parentId ?? string.Empty, parentId ?? string.Empty, StringComparison.Ordinal)
            && string.Equals(w.name?.Trim(), name, StringComparison.OrdinalIgnoreCase));

    private static int NextSort(List<PmWbsRow> items, string? parentId)
    {
        var siblings = items
            .Where(w => string.Equals(w.parentId ?? string.Empty, parentId ?? string.Empty, StringComparison.Ordinal))
            .ToList();
        return siblings.Count == 0 ? 10 : siblings.Max(s => s.sortOrder ?? 0) + 10;
    }

    private async Task UpsertProjectPackAsync(string projectId, JobPackDefinition pack, string token, CancellationToken ct)
    {
        var version = JobPackCatalog.NormalizeVersion(pack.Version);
        var existing = await FindProjectPackRowAsync(projectId, pack.Code, token, ct);
        var payload = new Dictionary<string, object?>
        {
            ["projectId"] = projectId,
            ["packCode"] = pack.Code,
            ["version"] = version,
            ["appliedAt"] = DateTime.UtcNow,
            ["appliedBy"] = EmptyToNull(_ctx.Username)
        };
        if (existing is not null && !string.IsNullOrWhiteSpace(existing.__dataId))
            await _dg.UpdateAsync(PmDatasets.ProjectPacks, existing.__dataId, payload, token, ct);
        else
            await _dg.CreateAsync(PmDatasets.ProjectPacks, payload, token, ct);
    }

    private async Task<List<PmProjectPackRow>> LoadProjectPackRowsAsync(string projectId, string token, CancellationToken ct)
    {
        var page = await _dg.QueryPageAsync(
            PmDatasets.ProjectPacks,
            new Dictionary<string, object?> { ["projectId"] = projectId },
            ListQuery,
            token,
            ct);
        return page.Items.Select(Map<PmProjectPackRow>).ToList();
    }

    private async Task<PmProjectPackRow?> FindProjectPackRowAsync(string projectId, string packCode, string token, CancellationToken ct)
    {
        var page = await _dg.QueryPageAsync(
            PmDatasets.ProjectPacks,
            new Dictionary<string, object?> { ["projectId"] = projectId, ["packCode"] = packCode },
            "limit=5&expand=false",
            token,
            ct);
        return page.Items.Select(Map<PmProjectPackRow>).FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.__dataId));
    }

    private static ProjectPackInstallDto ToInstallDto(PmProjectPackRow row)
    {
        var code = row.packCode ?? string.Empty;
        var version = JobPackCatalog.NormalizeVersion(row.version);
        var catalogVersion = JobPackCatalog.Find(code) is { } pack
            ? JobPackCatalog.NormalizeVersion(pack.Version)
            : version;
        return new ProjectPackInstallDto
        {
            PackCode = code,
            Version = version,
            AppliedAt = row.appliedAt,
            AppliedBy = row.appliedBy,
            Outdated = !string.Equals(version, catalogVersion, StringComparison.OrdinalIgnoreCase)
        };
    }
}
