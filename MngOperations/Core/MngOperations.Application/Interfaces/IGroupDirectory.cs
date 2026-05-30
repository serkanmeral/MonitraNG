using MngOperations.Application.Contracts.Runtime;

namespace MngOperations.Application.Interfaces;

/// <summary>
/// Person grup (Keeper grup) çözümlemesi — <see cref="IPersonDirectory"/> ile aynı desen (MO in-memory cache).
/// id → görünen ad map'i döner; eksik id'leri Keeper'dan (GET Group/{id}) çözüp TTL ile cache'ler.
/// </summary>
public interface IGroupDirectory
{
    Task<IReadOnlyDictionary<string, PersonDisplayDto>> GetGroupsAsync(
        IEnumerable<string> ids,
        string token,
        CancellationToken cancellationToken = default);
}
