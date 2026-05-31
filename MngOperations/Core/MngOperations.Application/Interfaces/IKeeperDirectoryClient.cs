using MngOperations.Application.Contracts.Runtime;

namespace MngOperations.Application.Interfaces;

/// <summary>
/// MngKeeper kullanıcı/grup dizini — Bearer forward. Toplu (by-ids) uçlar tek istekte çözer (N+1'i önler);
/// tekil metotlar geriye dönük uyumluluk/yedek olarak kalır. Cache <see cref="IPersonDirectory"/>/<see cref="IGroupDirectory"/>'de.
/// </summary>
public interface IKeeperDirectoryClient
{
    /// <summary>Kullanıcıyı id ile çözer; bulunamazsa veya yapılandırılmamışsa null döner.</summary>
    Task<PersonDisplayDto?> GetUserAsync(
        string userId,
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>Grubu id ile çözer (GET Group/{id}); bulunamazsa veya yapılandırılmamışsa null döner.</summary>
    Task<PersonDisplayDto?> GetGroupAsync(
        string groupId,
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Toplu kullanıcı çözümü (POST User/by-ids, tek istek). İstenen id (girişteki id, __dataId veya sub)
    /// → görünen ad eşlemesi döner; çözülemeyenler haritada bulunmaz (çağıran fallback uygular).
    /// </summary>
    Task<IReadOnlyDictionary<string, PersonDisplayDto>> GetUsersAsync(
        IEnumerable<string> ids,
        string token,
        CancellationToken cancellationToken = default);

    /// <summary>Toplu grup çözümü (POST Group/by-ids, tek istek). İstenen id → görünen ad eşlemesi.</summary>
    Task<IReadOnlyDictionary<string, PersonDisplayDto>> GetGroupsAsync(
        IEnumerable<string> ids,
        string token,
        CancellationToken cancellationToken = default);
}
