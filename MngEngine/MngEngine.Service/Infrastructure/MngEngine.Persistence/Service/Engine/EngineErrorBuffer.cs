using System.Collections.Concurrent;
using MngEngine.Application.Interfaces;

namespace MngEngine.Persistence.Service.Engine;

public class EngineErrorBuffer : IEngineErrorBuffer
{
    private const int MaxEntries = 100;
    private readonly ConcurrentQueue<EngineErrorEntry> _queue = new();

    public void Add(string assetId, string? agentId, string errorCode, string message)
    {
        var entry = new EngineErrorEntry(assetId, agentId ?? "unknown", errorCode, message, DateTime.UtcNow);
        _queue.Enqueue(entry);
        while (_queue.Count > MaxEntries && _queue.TryDequeue(out _)) { }
    }

    public IReadOnlyList<EngineErrorEntry> GetRecent(int count = 50)
    {
        var items = _queue.ToArray();
        if (items.Length == 0) return [];
        var take = Math.Min(count, items.Length);
        return items.Skip(Math.Max(0, items.Length - take)).ToList();
    }
}
