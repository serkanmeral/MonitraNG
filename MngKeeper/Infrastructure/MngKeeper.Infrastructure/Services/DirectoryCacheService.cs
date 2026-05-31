using MngKeeper.Application.Features.Group.Queries.GetGroupsByIds;
using MngKeeper.Application.Features.User.Queries.GetUsersByIds;
using MngKeeper.Application.Interfaces;

namespace MngKeeper.Infrastructure.Services
{
    /// <summary>
    /// <see cref="IDirectoryCache"/> — Redis (IRedisService) üzerinde dizin profil cache'i.
    /// Anahtar şeması: <c>oc:dir:user:{domain}:{id}</c> / <c>oc:dir:group:{domain}:{id}</c> (RedisService ayrıca
    /// <c>mngkeeper:</c> ön ekini ekler). IRedisService fail-open olduğundan Redis düşse de sorun çıkmaz.
    /// </summary>
    public sealed class DirectoryCacheService : IDirectoryCache
    {
        // by-ids invalidation'ı kaçırsa bile bayat veriyi sınırlamak için makul bir güvenlik TTL'i.
        private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

        private readonly IRedisService _redis;

        public DirectoryCacheService(IRedisService redis)
        {
            _redis = redis;
        }

        private static string UserKey(string domainId, string id) => $"oc:dir:user:{domainId}:{id}";
        private static string GroupKey(string domainId, string id) => $"oc:dir:group:{domainId}:{id}";

        public async Task<IReadOnlyDictionary<string, UserLookupItemDto>> GetUsersAsync(string domainId, IEnumerable<string> ids)
        {
            var result = new Dictionary<string, UserLookupItemDto>(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                if (string.IsNullOrWhiteSpace(id))
                    continue;
                var item = await _redis.GetAsync<UserLookupItemDto>(UserKey(domainId, id));
                if (item != null)
                    result[id] = item;
            }
            return result;
        }

        public async Task SetUsersAsync(string domainId, IEnumerable<UserLookupItemDto> items)
        {
            foreach (var item in items)
            {
                if (item == null)
                    continue;
                if (!string.IsNullOrWhiteSpace(item.UserId))
                    await _redis.SetAsync(UserKey(domainId, item.UserId), item, Ttl);
                if (!string.IsNullOrWhiteSpace(item.KeycloakUserId))
                    await _redis.SetAsync(UserKey(domainId, item.KeycloakUserId!), item, Ttl);
            }
        }

        public async Task InvalidateUserAsync(string domainId, string? dataId, string? keycloakUserId)
        {
            if (!string.IsNullOrWhiteSpace(dataId))
                await _redis.DeleteAsync(UserKey(domainId, dataId!));
            if (!string.IsNullOrWhiteSpace(keycloakUserId))
                await _redis.DeleteAsync(UserKey(domainId, keycloakUserId!));
        }

        public async Task<IReadOnlyDictionary<string, GroupLookupItemDto>> GetGroupsAsync(string domainId, IEnumerable<string> ids)
        {
            var result = new Dictionary<string, GroupLookupItemDto>(StringComparer.Ordinal);
            foreach (var id in ids)
            {
                if (string.IsNullOrWhiteSpace(id))
                    continue;
                var item = await _redis.GetAsync<GroupLookupItemDto>(GroupKey(domainId, id));
                if (item != null)
                    result[id] = item;
            }
            return result;
        }

        public async Task SetGroupsAsync(string domainId, IEnumerable<GroupLookupItemDto> items)
        {
            foreach (var item in items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.GroupId))
                    continue;
                await _redis.SetAsync(GroupKey(domainId, item.GroupId), item, Ttl);
            }
        }

        public async Task InvalidateGroupAsync(string domainId, string groupId)
        {
            if (!string.IsNullOrWhiteSpace(groupId))
                await _redis.DeleteAsync(GroupKey(domainId, groupId));
        }
    }
}
