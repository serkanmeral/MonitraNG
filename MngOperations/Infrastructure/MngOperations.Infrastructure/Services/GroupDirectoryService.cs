using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngOperations.Application.Configuration;
using MngOperations.Application.Contracts.Runtime;
using MngOperations.Application.Interfaces;

namespace MngOperations.Infrastructure.Services;

/// <summary>
/// Person grup (Keeper grup) çözümlemesi — <see cref="PersonDirectoryService"/> ile aynı desen.
/// Eksik id'ler Keeper'dan (GET Group/{id}) tekil çözülüp TTL ile cache'lenir (negatif sonuç da cache'lenir).
/// </summary>
public sealed class GroupDirectoryService : IGroupDirectory
{
    private readonly IMemoryCache _cache;
    private readonly IKeeperDirectoryClient _keeper;
    private readonly IRequestContext _requestContext;
    private readonly ILogger<GroupDirectoryService> _logger;
    private readonly TimeSpan _ttl;

    public GroupDirectoryService(
        IMemoryCache cache,
        IKeeperDirectoryClient keeper,
        IRequestContext requestContext,
        ILogger<GroupDirectoryService> logger,
        IOptions<MngOperationsSettings> settings)
    {
        _cache = cache;
        _keeper = keeper;
        _requestContext = requestContext;
        _logger = logger;
        _ttl = TimeSpan.FromSeconds(Math.Max(30, settings.Value.MetadataCache.PersonTtlSeconds));
    }

    public async Task<IReadOnlyDictionary<string, PersonDisplayDto>> GetGroupsAsync(
        IEnumerable<string> ids,
        string token,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, PersonDisplayDto>(StringComparer.Ordinal);
        var distinct = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (distinct.Count == 0)
            return result;

        var missing = new List<string>();
        foreach (var id in distinct)
        {
            if (_cache.TryGetValue(CacheKey(id), out PersonDisplayDto? cached) && cached != null)
                result[id] = cached;
            else
                missing.Add(id);
        }

        if (missing.Count > 0)
        {
            var resolved = await Task.WhenAll(missing.Select(async id =>
            {
                var group = await _keeper.GetGroupAsync(id, token, cancellationToken)
                    ?? new PersonDisplayDto { Id = id, Name = id };
                return group;
            }));

            foreach (var group in resolved)
            {
                _cache.Set(CacheKey(group.Id), group, _ttl);
                result[group.Id] = group;
            }

            _logger.LogDebug("Group directory resolved {Count} new id(s)", missing.Count);
        }

        return result;
    }

    private string CacheKey(string id) =>
        $"oc:{_requestContext.DomainId ?? "unknown"}:group:{id}";
}
