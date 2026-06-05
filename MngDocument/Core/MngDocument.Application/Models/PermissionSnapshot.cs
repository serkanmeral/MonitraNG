using MngDocument.Application.Contracts.Resources;
using MngDocument.Application.Exceptions;
using MngDocument.Domain.Constants;

namespace MngDocument.Application.Models;

/// <summary>
/// Geçerli kullanıcı için yetki çözümünün anlık görüntüsü: tüm klasörler (anchor tespiti için)
/// + tüm grup izin kayıtları bellekte tutulur, böylece tree/children/search gibi çoklu kaynak
/// filtrelemesinde tekrar DG sorgusu yapılmadan etkin yetki hesaplanır.
///
/// Model (SharePoint benzeri): bir kaynağın etkin yetkisi, kendisi + <c>ancestorIds</c> zincirinde
/// tabandan yukarı en yakın <c>permissionsBroken=true</c> klasörün (anchor) ACL'idir. Zincirde hiç
/// anchor yoksa açık varsayılan (tüm aksiyonlar serbest) uygulanır. Admin daima tam yetkilidir.
/// Manager (<c>isManager</c> JWT): mirası kırık bir anchor altında <c>view</c> yetkisi olan kaynaklarda tam yetki
/// (Manager klasörü gibi kısıtlı alanlarda menü/CRUD — admin bypass ile aynı mantık, kapsam dar).
/// </summary>
public sealed class PermissionSnapshot
{
    private readonly Dictionary<string, DmResource> _foldersById;
    private readonly Dictionary<string, List<DmResourcePermission>> _permsByAnchor;
    private readonly HashSet<string> _userGroups;
    private readonly bool _isAdmin;
    private readonly bool _isManager;

    public PermissionSnapshot(
        IReadOnlyList<DmResource> allFolders,
        IReadOnlyList<DmResourcePermission> allPermissions,
        IEnumerable<string> userGroups,
        bool isAdmin,
        bool isManager = false)
    {
        AllFolders = allFolders;
        _foldersById = allFolders
            .Where(f => f.__dataId is not null)
            .GroupBy(f => f.__dataId!)
            .ToDictionary(g => g.Key, g => g.First());

        _permsByAnchor = allPermissions
            .Where(p => !string.IsNullOrWhiteSpace(p.resourceId))
            .GroupBy(p => p.resourceId!)
            .ToDictionary(g => g.Key, g => g.ToList());

        _userGroups = new HashSet<string>(
            userGroups.Where(g => !string.IsNullOrWhiteSpace(g)),
            StringComparer.OrdinalIgnoreCase);
        _isAdmin = isAdmin;
        _isManager = isManager;
    }

    /// <summary>Tüm klasörler (tree kurulumunda yeniden yüklemeden kullanmak için).</summary>
    public IReadOnlyList<DmResource> AllFolders { get; }

    public bool IsAdmin => _isAdmin;

    /// <summary>Bir anchor klasöre bağlı grup izin kayıtları (yoksa boş).</summary>
    public IReadOnlyList<DmResourcePermission> GetRecords(string anchorId) =>
        _permsByAnchor.TryGetValue(anchorId, out var list)
            ? list
            : Array.Empty<DmResourcePermission>();

    /// <summary>
    /// Bir kaynağın etkin yetkisini geldiği anchor klasör id'sini döndürür.
    /// Anchor yoksa (açık varsayılan) <c>null</c>.
    /// </summary>
    public string? ResolveAnchorId(DmResource resource)
    {
        if (resource.type == ResourceType.Folder && resource.__dataId is not null
            && IsBroken(resource.__dataId, resource))
        {
            return resource.__dataId;
        }

        var ancestors = resource.ancestorIds;
        if (ancestors is not null)
        {
            for (var i = ancestors.Count - 1; i >= 0; i--)
            {
                var id = ancestors[i];
                if (!string.IsNullOrWhiteSpace(id) && IsBroken(id, null))
                    return id;
            }
        }

        return null;
    }

    /// <summary>Geçerli kullanıcının kaynak üzerindeki etkin yetkilerini çözer.</summary>
    public EffectivePermissionDto Resolve(DmResource resource)
    {
        if (_isAdmin)
            return EffectivePermissionDto.Full;

        var anchorId = ResolveAnchorId(resource);
        if (anchorId is null)
            return EffectivePermissionDto.Full; // açık varsayılan

        var fromAnchor = ResolveFromAnchor(anchorId);

        // Kısıtlı (mirası kırık) alan: manager kullanıcı görüntüleyebiliyorsa tam yetki (UI menü + CRUD).
        if (_isManager && fromAnchor.CanView)
            return EffectivePermissionDto.Full;

        return fromAnchor;
    }

    private EffectivePermissionDto ResolveFromAnchor(string anchorId)
    {
        var granted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (_permsByAnchor.TryGetValue(anchorId, out var records))
        {
            foreach (var rec in records)
            {
                if (rec.groupName is null || !_userGroups.Contains(rec.groupName) || rec.permissions is null)
                    continue;
                foreach (var p in rec.permissions)
                    granted.Add(p);
            }
        }

        return new EffectivePermissionDto
        {
            CanView = granted.Contains(ResourceAction.View),
            CanCreate = granted.Contains(ResourceAction.Create),
            CanEdit = granted.Contains(ResourceAction.Edit),
            CanDelete = granted.Contains(ResourceAction.Delete),
            CanUpload = granted.Contains(ResourceAction.Upload),
            CanDownload = granted.Contains(ResourceAction.Download),
            CanMove = granted.Contains(ResourceAction.Move),
            CanShare = granted.Contains(ResourceAction.Share)
        };
    }

    /// <summary>Kullanıcının kaynak üzerinde belirtilen aksiyona yetkisi yoksa 403 fırlatır.</summary>
    public void EnsureCan(DmResource resource, string action)
    {
        if (Allows(Resolve(resource), action))
            return;
        throw DocumentException.Forbidden();
    }

    /// <summary>İzin yönetimi (set/break/restore) yetkisi: admin ya da etkin <c>share</c>.</summary>
    public void EnsureCanManage(DmResource resource)
    {
        if (_isAdmin || Resolve(resource).CanShare)
            return;
        throw DocumentException.Forbidden(
            "Klasör yetkilerini yönetme izniniz yok.",
            "You are not allowed to manage permissions for this folder.");
    }

    public static bool Allows(EffectivePermissionDto eff, string action) => action switch
    {
        ResourceAction.View => eff.CanView,
        ResourceAction.Create => eff.CanCreate,
        ResourceAction.Edit => eff.CanEdit,
        ResourceAction.Delete => eff.CanDelete,
        ResourceAction.Upload => eff.CanUpload,
        ResourceAction.Download => eff.CanDownload,
        ResourceAction.Move => eff.CanMove,
        ResourceAction.Share => eff.CanShare,
        _ => false
    };

    private bool IsBroken(string folderId, DmResource? known)
    {
        if (known is not null && string.Equals(known.__dataId, folderId, StringComparison.Ordinal))
            return known.permissionsBroken == true;

        return _foldersById.TryGetValue(folderId, out var folder) && folder.permissionsBroken == true;
    }
}
