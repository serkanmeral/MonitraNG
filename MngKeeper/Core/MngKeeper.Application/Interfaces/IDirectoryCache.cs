using MngKeeper.Application.Features.Group.Queries.GetGroupsByIds;
using MngKeeper.Application.Features.User.Queries.GetUsersByIds;

namespace MngKeeper.Application.Interfaces
{
    /// <summary>
    /// MO dizin çözümü (by-ids) için Redis destekli profil cache'i — yalnızca görünen alanlar
    /// (ad/başlık/aktif). Kullanıcı hem <c>__dataId</c> hem Keycloak <c>sub</c> anahtarı altında yazılır
    /// (MO her iki kimlikle de isteyebilir). CRUD'da (update/delete) ilgili anahtarlar geçersiz kılınır.
    /// Tüm işlemler best-effort/fail-open: Redis erişilemezse sessizce Mongo'ya düşülür.
    /// </summary>
    public interface IDirectoryCache
    {
        /// <summary>Verilen id'ler için cache'teki kullanıcıları döner (anahtar = istenen id).</summary>
        Task<IReadOnlyDictionary<string, UserLookupItemDto>> GetUsersAsync(string domainId, IEnumerable<string> ids);

        /// <summary>Kullanıcıları cache'e yazar (her biri __dataId ve keycloak sub anahtarları altında).</summary>
        Task SetUsersAsync(string domainId, IEnumerable<UserLookupItemDto> items);

        /// <summary>Bir kullanıcının cache'ini geçersiz kılar (her iki kimlik anahtarı).</summary>
        Task InvalidateUserAsync(string domainId, string? dataId, string? keycloakUserId);

        /// <summary>Verilen id'ler için cache'teki grupları döner.</summary>
        Task<IReadOnlyDictionary<string, GroupLookupItemDto>> GetGroupsAsync(string domainId, IEnumerable<string> ids);

        /// <summary>Grupları cache'e yazar.</summary>
        Task SetGroupsAsync(string domainId, IEnumerable<GroupLookupItemDto> items);

        /// <summary>Bir grubun cache'ini geçersiz kılar.</summary>
        Task InvalidateGroupAsync(string domainId, string groupId);
    }
}
