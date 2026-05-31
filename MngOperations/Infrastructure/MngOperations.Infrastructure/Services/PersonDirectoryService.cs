using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngOperations.Application.Configuration;
using MngOperations.Application.Contracts.Runtime;
using MngOperations.Application.Diagnostics;
using MngOperations.Application.Interfaces;

namespace MngOperations.Infrastructure.Services;

/// <summary>
/// Person (Keeper kullanıcı) çözümlemesi — kataloglar gibi domain-scoped in-memory cache.
/// Eksik id'ler Keeper'dan tekil çözülüp TTL ile cache'lenir (negatif sonuç da cache'lenir).
/// </summary>
public sealed class PersonDirectoryService : IPersonDirectory
{
    private readonly IMemoryCache _cache;
    private readonly IKeeperDirectoryClient _keeper;
    private readonly IRequestContext _requestContext;
    private readonly ILogger<PersonDirectoryService> _logger;
    private readonly OcCallStats _stats;
    private readonly TimeSpan _ttl;

    public PersonDirectoryService(
        IMemoryCache cache,
        IKeeperDirectoryClient keeper,
        IRequestContext requestContext,
        ILogger<PersonDirectoryService> logger,
        IOptions<MngOperationsSettings> settings,
        OcCallStats stats)
    {
        _cache = cache;
        _keeper = keeper;
        _requestContext = requestContext;
        _logger = logger;
        _stats = stats;
        _ttl = TimeSpan.FromSeconds(Math.Max(30, settings.Value.MetadataCache.PersonTtlSeconds));
    }

    public async Task<IReadOnlyDictionary<string, PersonDisplayDto>> GetPeopleAsync(
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
            // Toplu by-ids: id başına çağrı yerine tek Keeper isteği (N+1 giderildi). Ölçüm: 1 çağrı / N id.
            var sw = Stopwatch.StartNew();
            var resolved = await _keeper.GetUsersAsync(missing, token, cancellationToken);
            sw.Stop();
            _stats.RecordKeeper(1, sw.ElapsedMilliseconds);

            foreach (var id in missing)
            {
                // Çözülemeyenler için negatif sonuç da cache'lenir (id=ad fallback) — tekrarlı istek olmasın.
                var person = resolved.TryGetValue(id, out var p) && p != null
                    ? p
                    : new PersonDisplayDto { Id = id, Name = id };
                _cache.Set(CacheKey(id), person, _ttl);
                result[id] = person;
            }

            _logger.LogDebug("Person directory resolved {Count} new id(s) via by-ids", missing.Count);
        }

        return result;
    }

    private string CacheKey(string id) =>
        $"oc:{_requestContext.DomainId ?? "unknown"}:person:{id}";
}
