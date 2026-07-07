namespace MngDocument.Application.Models;

/// <summary>
/// <see cref="Interfaces.IPermissionService.LoadSnapshotAsync"/> yükleme kapsamı.
/// </summary>
public enum PermissionSnapshotScope
{
    /// <summary>
    /// Tüm izin kayıtları + yalnızca anchor klasörler (<c>permissionsBroken=true</c>).
    /// Çoğu API uç noktası için yeterli; tüm klasör listesi çekilmez.
    /// </summary>
    Lean,

    /// <summary>
    /// Tüm izin kayıtları + tüm klasörler. Yalnızca legacy tam ağaç (<c>GET /tree</c>) için.
    /// </summary>
    Full
}
