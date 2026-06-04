using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using MngEngine.Application.Features.SecEvents;
using MngEngine.Application.Interfaces;
using MngEngine.Persistence.Options;

namespace MngEngine.Persistence.Service.SecEvents;

public sealed class SecEventBatchQueue : ISecEventBatchQueue
{
    private readonly ConcurrentQueue<SecEventIngestItem> _queue = new();
    private readonly int _maxItems;
    private readonly object _sync = new();

    public SecEventBatchQueue(IOptions<SecEventQueueOptions> options)
    {
        var max = options.Value.MaxItems;
        _maxItems = max > 0 ? max : 0;
    }

    public void Enqueue(SecEventIngestItem item)
    {
        if (item == null)
            return;

        lock (_sync)
        {
            while (_maxItems > 0 && _queue.Count >= _maxItems && _queue.TryDequeue(out _))
            {
            }

            _queue.Enqueue(item);
        }
    }

    public IReadOnlyList<SecEventIngestItem> DequeueAll()
    {
        lock (_sync)
        {
            var list = new List<SecEventIngestItem>();
            while (_queue.TryDequeue(out var item))
                list.Add(item);

            return list;
        }
    }

    public int Count
    {
        get
        {
            lock (_sync)
                return _queue.Count;
        }
    }

    public IReadOnlyList<SecEventIngestItem> PeekAll()
    {
        lock (_sync)
            return _queue.ToArray();
    }
}
