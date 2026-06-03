using System.Text.Json;
using Microsoft.Extensions.Logging;
using MngDocument.Application.Contracts.Resources;
using MngDocument.Application.Exceptions;
using MngDocument.Application.Interfaces;
using MngDocument.Application.Models;
using MngDocument.Domain.Constants;

namespace MngDocument.Infrastructure.Services;

/// <summary>
/// Grup bazlı klasör yetkilendirmesi + miras. Kayıtlar <c>dm_resource_permissions</c>'ta anchor
/// (mirası kırık) klasörlere bağlıdır. Çözüm <see cref="PermissionSnapshot"/> üzerinden yapılır.
/// </summary>
public sealed class PermissionService : IPermissionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string ListQuery = "limit=1000&expand=false&showHistory=true";

    /// <summary>Yetki snapshot'ı için history gerekmez (payload küçültme).</summary>
    private const string SnapshotListQuery = "limit=1000&expand=false&showHistory=false";

    private readonly IMngDataGatewayClient _dg;
    private readonly IRequestContext _ctx;
    private readonly ILogger<PermissionService> _logger;
    private PermissionSnapshot? _requestSnapshot;

    public PermissionService(IMngDataGatewayClient dg, IRequestContext ctx, ILogger<PermissionService> logger)
    {
        _dg = dg;
        _ctx = ctx;
        _logger = logger;
    }

    private string? Token => _ctx.BearerToken;

    public void InvalidateSnapshotCache() => _requestSnapshot = null;

    public async Task<PermissionSnapshot> LoadSnapshotAsync(CancellationToken ct = default)
    {
        if (_requestSnapshot is not null)
            return _requestSnapshot;

        var folderPage = await _dg.QueryPageAsync(
            DmDatasets.Resources,
            new Dictionary<string, object?> { ["type"] = ResourceType.Folder },
            SnapshotListQuery,
            Token,
            ct);

        var permPage = await _dg.QueryPageAsync(
            DmDatasets.ResourcePermissions,
            new Dictionary<string, object?>(),
            SnapshotListQuery,
            Token,
            ct);

        var folders = folderPage.Items.Select(MapResource).ToList();
        var perms = permPage.Items.Select(MapPermission).ToList();

        _requestSnapshot = new PermissionSnapshot(folders, perms, _ctx.UserGroups, _ctx.IsAdmin);
        return _requestSnapshot;
    }

    public async Task<FolderPermissionsDto> GetFolderPermissionsAsync(string folderId, CancellationToken ct = default)
    {
        var folder = await LoadFolderOrThrowAsync(folderId, ct);
        var snapshot = await LoadSnapshotAsync(ct);
        snapshot.EnsureCan(folder, ResourceAction.View);
        return BuildFolderPermissionsDto(folder, snapshot);
    }

    public async Task<FolderPermissionsDto> SetFolderPermissionsAsync(
        string folderId, SetFolderPermissionsRequest request, CancellationToken ct = default)
    {
        var folder = await LoadFolderOrThrowAsync(folderId, ct);
        var snapshot = await LoadSnapshotAsync(ct);
        snapshot.EnsureCanManage(folder);

        if (folder.permissionsBroken != true)
        {
            throw DocumentException.Validation(
                "INHERITANCE_NOT_BROKEN",
                "Break inheritance before setting folder permissions.",
                "Klasör yetkilerini belirlemeden önce yetki mirasını kırın.");
        }

        var normalized = NormalizeGroups(request.Groups);
        await ReplaceFolderPermissionsAsync(folderId, normalized, ct);

        InvalidateSnapshotCache();
        var refreshed = await LoadSnapshotAsync(ct);
        return BuildFolderPermissionsDto(folder, refreshed);
    }

    public async Task<FolderPermissionsDto> BreakInheritanceAsync(string folderId, CancellationToken ct = default)
    {
        var folder = await LoadFolderOrThrowAsync(folderId, ct);
        var snapshot = await LoadSnapshotAsync(ct);
        snapshot.EnsureCanManage(folder);

        if (folder.permissionsBroken == true)
            return BuildFolderPermissionsDto(folder, snapshot);

        // Üst zincirdeki en yakın anchor'ın ACL'ini kopyala (yoksa açık -> boş başla).
        var parentAnchorId = snapshot.ResolveAnchorId(folder);
        var toCreate = new Dictionary<string, GroupPermissionInput>(StringComparer.OrdinalIgnoreCase);
        if (parentAnchorId is not null)
        {
            foreach (var rec in snapshot.GetRecords(parentAnchorId))
            {
                if (string.IsNullOrWhiteSpace(rec.groupName))
                    continue;
                toCreate[rec.groupName!] = new GroupPermissionInput
                {
                    GroupId = rec.groupId,
                    GroupName = rec.groupName!,
                    Permissions = (rec.permissions ?? new List<string>()).ToList()
                };
            }
        }

        // Kilitlenmeyi önle: işlemi yapan (admin değilse) bu klasörü göremeyecekse kendi
        // gruplarına tam yetki ver — böylece yönetimi sürdürebilir.
        if (!snapshot.IsAdmin)
        {
            var actorGroups = _ctx.UserGroups.Where(g => !string.IsNullOrWhiteSpace(g)).ToList();
            var actorHasView = toCreate.Values.Any(g =>
                actorGroups.Contains(g.GroupName, StringComparer.OrdinalIgnoreCase)
                && g.Permissions.Contains(ResourceAction.View, StringComparer.OrdinalIgnoreCase));

            if (!actorHasView && actorGroups.Count > 0)
            {
                foreach (var g in actorGroups)
                {
                    toCreate[g] = new GroupPermissionInput
                    {
                        GroupName = g,
                        Permissions = ResourceAction.All.ToList()
                    };
                }
            }
        }

        await SetBrokenFlagAsync(folderId, true, ct);
        await ReplaceFolderPermissionsAsync(folderId, NormalizeGroups(toCreate.Values.ToList()), ct);

        folder.permissionsBroken = true;
        InvalidateSnapshotCache();
        var refreshed = await LoadSnapshotAsync(ct);
        return BuildFolderPermissionsDto(folder, refreshed);
    }

    public async Task<FolderPermissionsDto> RestoreInheritanceAsync(string folderId, CancellationToken ct = default)
    {
        var folder = await LoadFolderOrThrowAsync(folderId, ct);
        var snapshot = await LoadSnapshotAsync(ct);
        snapshot.EnsureCanManage(folder);

        if (folder.permissionsBroken != true)
            return BuildFolderPermissionsDto(folder, snapshot);

        await DeleteFolderPermissionsAsync(folderId, ct);
        await SetBrokenFlagAsync(folderId, false, ct);

        folder.permissionsBroken = false;
        InvalidateSnapshotCache();
        var refreshed = await LoadSnapshotAsync(ct);
        return BuildFolderPermissionsDto(folder, refreshed);
    }

    /// <summary>Bir klasör silindiğinde ona bağlı izin kayıtlarını temizler (ResourceService kullanır).</summary>
    public async Task DeleteFolderPermissionsAsync(string folderId, CancellationToken ct = default)
    {
        var match = new Dictionary<string, object?> { ["resourceId"] = folderId };
        var page = await _dg.QueryPageAsync(DmDatasets.ResourcePermissions, match, SnapshotListQuery, Token, ct);
        InvalidateSnapshotCache();
        foreach (var row in page.Items)
        {
            if (row.TryGetValue("__dataId", out var idVal) && idVal is not null)
            {
                var id = GetString(idVal);
                if (!string.IsNullOrEmpty(id))
                    await _dg.DeleteAsync(DmDatasets.ResourcePermissions, id, Token, ct);
            }
        }
    }

    // ----- helpers -----

    private FolderPermissionsDto BuildFolderPermissionsDto(DmResource folder, PermissionSnapshot snapshot)
    {
        var broken = folder.permissionsBroken == true;
        string? anchorId = broken ? folder.__dataId : snapshot.ResolveAnchorId(folder);

        IReadOnlyList<GroupPermissionDto> groups = anchorId is null
            ? Array.Empty<GroupPermissionDto>()
            : snapshot.GetRecords(anchorId)
                .Where(r => !string.IsNullOrWhiteSpace(r.groupName))
                .Select(r => new GroupPermissionDto
                {
                    GroupId = r.groupId,
                    GroupName = r.groupName!,
                    Permissions = r.permissions ?? new List<string>()
                })
                .OrderBy(g => g.GroupName, StringComparer.OrdinalIgnoreCase)
                .ToList();

        return new FolderPermissionsDto
        {
            ResourceId = folder.__dataId ?? string.Empty,
            InheritanceBroken = broken,
            EffectiveAnchorId = anchorId,
            Groups = groups,
            Effective = snapshot.Resolve(folder)
        };
    }

    /// <summary>Grup girdilerini doğrular/normalize eder: geçersiz aksiyonları atar, herhangi bir
    /// aksiyon varsa <c>view</c> ekler, boş/grupsuz kayıtları düşürür, grup adına göre tekilleştirir.</summary>
    private static List<GroupPermissionInput> NormalizeGroups(IEnumerable<GroupPermissionInput> groups)
    {
        var result = new Dictionary<string, GroupPermissionInput>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in groups)
        {
            var name = g.GroupName?.Trim();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var actions = (g.Permissions ?? new List<string>())
                .Where(ResourceAction.IsValid)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (actions.Count == 0)
                continue;

            if (!actions.Contains(ResourceAction.View, StringComparer.OrdinalIgnoreCase))
                actions.Insert(0, ResourceAction.View);

            result[name] = new GroupPermissionInput
            {
                GroupId = g.GroupId,
                GroupName = name,
                Permissions = actions
            };
        }

        return result.Values.ToList();
    }

    private async Task ReplaceFolderPermissionsAsync(string folderId, List<GroupPermissionInput> groups, CancellationToken ct)
    {
        await DeleteFolderPermissionsAsync(folderId, ct);

        foreach (var g in groups)
        {
            var payload = new Dictionary<string, object?>
            {
                ["resourceId"] = folderId,
                ["groupId"] = g.GroupId,
                ["groupName"] = g.GroupName,
                ["permissions"] = g.Permissions
            };
            await _dg.CreateAsync<Dictionary<string, object?>>(DmDatasets.ResourcePermissions, payload, Token, ct);
        }
    }

    private async Task SetBrokenFlagAsync(string folderId, bool broken, CancellationToken ct)
    {
        var payload = new Dictionary<string, object?> { ["permissionsBroken"] = broken };
        await _dg.UpdateAsync<DmResource>(DmDatasets.Resources, folderId, payload, Token, ct);
    }

    private async Task<DmResource> LoadFolderOrThrowAsync(string folderId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(folderId))
            throw DocumentException.NotFound();

        var folder = await _dg.GetByIdAsync<DmResource>(DmDatasets.Resources, folderId, Token, ct);
        if (folder is null || folder.__dataId is null)
            throw DocumentException.NotFound();

        if (folder.type != ResourceType.Folder)
        {
            throw DocumentException.Validation(
                "NOT_FOLDER",
                "Permissions can only be managed on folders.",
                "Yetkiler yalnızca klasörlerde yönetilebilir.");
        }

        return folder;
    }

    private static DmResource MapResource(Dictionary<string, object?> row)
    {
        var json = JsonSerializer.Serialize(row, JsonOptions);
        return JsonSerializer.Deserialize<DmResource>(json, JsonOptions) ?? new DmResource();
    }

    private static DmResourcePermission MapPermission(Dictionary<string, object?> row)
    {
        var json = JsonSerializer.Serialize(row, JsonOptions);
        return JsonSerializer.Deserialize<DmResourcePermission>(json, JsonOptions) ?? new DmResourcePermission();
    }

    private static string? GetString(object? value) => value switch
    {
        null => null,
        string s => s,
        JsonElement { ValueKind: JsonValueKind.String } el => el.GetString(),
        _ => value.ToString()
    };
}
