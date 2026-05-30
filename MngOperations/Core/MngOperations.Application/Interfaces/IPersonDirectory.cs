using MngOperations.Application.Contracts.Runtime;

namespace MngOperations.Application.Interfaces;

/// <summary>
/// Person (Keeper kullanıcı) çözümlemesi — kataloglar gibi MO in-memory cache'i.
/// id → görünen ad map'i döner; eksik id'leri Keeper'dan çözüp TTL ile cache'ler.
/// </summary>
public interface IPersonDirectory
{
    Task<IReadOnlyDictionary<string, PersonDisplayDto>> GetPeopleAsync(
        IEnumerable<string> ids,
        string token,
        CancellationToken cancellationToken = default);
}
