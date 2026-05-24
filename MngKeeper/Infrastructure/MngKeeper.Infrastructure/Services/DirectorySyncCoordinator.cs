using System.Collections.Concurrent;
using MngKeeper.Application.Interfaces;

namespace MngKeeper.Infrastructure.Services;

public sealed class DirectorySyncCoordinator : IDirectorySyncCoordinator
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);

    public bool TryBeginSync(string domainId)
    {
        if (string.IsNullOrWhiteSpace(domainId))
            return false;

        var sem = _locks.GetOrAdd(domainId, _ => new SemaphoreSlim(1, 1));
        return sem.Wait(0);
    }

    public void EndSync(string domainId)
    {
        if (string.IsNullOrWhiteSpace(domainId))
            return;

        if (_locks.TryGetValue(domainId, out var sem))
            sem.Release();
    }
}
