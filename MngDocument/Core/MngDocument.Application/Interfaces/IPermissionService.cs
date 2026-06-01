using MngDocument.Application.Contracts.Resources;
using MngDocument.Application.Models;

namespace MngDocument.Application.Interfaces;

/// <summary>
/// Document Intelligence grup bazlı klasör yetkilendirmesi + miras. Yetkiler
/// <c>dm_resource_permissions</c>'ta anchor (mirası kırık) klasörlerde tutulur; dosya/markdown
/// içinde bulunduğu klasörün etkin yetkisini miras alır. Eşleştirme grup adı ile yapılır.
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// Geçerli kullanıcı için tüm klasör + izin kayıtlarını tek seferde yükleyip bellekte
    /// çözüm yapan anlık görüntü kurar (tree/children/search filtreleme + tek kaynak kontrolü).
    /// </summary>
    Task<PermissionSnapshot> LoadSnapshotAsync(CancellationToken ct = default);

    /// <summary>Bir klasörün yetki yönetim görünümü (miras durumu + grup matrisi + etkin yetki).</summary>
    Task<FolderPermissionsDto> GetFolderPermissionsAsync(string folderId, CancellationToken ct = default);

    /// <summary>Anchor (mirası kırık) klasörde grup yetki matrisini değiştirir (tam değişim).</summary>
    Task<FolderPermissionsDto> SetFolderPermissionsAsync(string folderId, SetFolderPermissionsRequest request, CancellationToken ct = default);

    /// <summary>Klasörün yetki mirasını kırar (üst anchor'ın ACL'ini kopyalar, anchor yapar).</summary>
    Task<FolderPermissionsDto> BreakInheritanceAsync(string folderId, CancellationToken ct = default);

    /// <summary>Klasörün kendi ACL'ini silip yetki mirasını geri yükler.</summary>
    Task<FolderPermissionsDto> RestoreInheritanceAsync(string folderId, CancellationToken ct = default);

    /// <summary>Bir klasöre bağlı tüm izin kayıtlarını siler (kaynak silinirken temizlik).</summary>
    Task DeleteFolderPermissionsAsync(string folderId, CancellationToken ct = default);
}
