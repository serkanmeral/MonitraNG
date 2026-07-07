namespace MngDocument.Application.Models;

/// <summary>
/// Domain genelinde izin kataloğu (klasörler + ACL satırları).
/// Kullanıcıya özel <see cref="PermissionSnapshot"/> bu veriden + istek bağlamından üretilir.
/// </summary>
public sealed record PermissionCatalogData(
    IReadOnlyList<DmResource> Folders,
    IReadOnlyList<DmResourcePermission> Permissions);
