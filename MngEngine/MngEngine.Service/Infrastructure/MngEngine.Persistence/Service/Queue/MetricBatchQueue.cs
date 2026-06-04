using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using MngEngine.Application.Features.Ingest;
using MngEngine.Application.Interfaces;

namespace MngEngine.Persistence.Service.Queue;

public class MetricBatchQueue : IMetricBatchQueue
{
    private readonly ConcurrentQueue<IngestBatch> _queue = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastCollectedByAsset = new(StringComparer.OrdinalIgnoreCase);
    private readonly int _maxBatches;
    private readonly object _sync = new();

    public MetricBatchQueue(IOptions<QueueOptions> options)
    {
        var max = options?.Value?.MaxBatches ?? 1000;
        _maxBatches = max > 0 ? max : 0; // 0 = sınırsız
    }

    public void Enqueue(IngestBatch batch)
    {
        if (batch?.Metrics?.Count == 0)
            return;

        lock (_sync)
        {
            if (!string.IsNullOrEmpty(batch.AssetId))
                _lastCollectedByAsset[batch.AssetId] = batch.CollectedAt;

            // Limit varsa ve aşıldıysa en eskileri at, en yenileri tut
            while (_maxBatches > 0 && _queue.Count >= _maxBatches && _queue.TryDequeue(out _))
            {
                // En eski batch atıldı
            }

            _queue.Enqueue(batch);
        }
    }

    public IReadOnlyList<IngestBatch> DequeueAll()
    {
        lock (_sync)
        {
            var list = new List<IngestBatch>();
            while (_queue.TryDequeue(out var batch))
                list.Add(batch);
            return list;
        }
    }

    public int Count
    {
        get
        {
            lock (_sync)
            {
                return _queue.Count;
            }
        }
    }

    public IReadOnlyList<IngestBatch> PeekAll()
    {
        lock (_sync)
        {
            return _queue.ToArray();
        }
    }

    public IReadOnlyDictionary<string, DateTime> GetLastCollectedByAsset()
    {
        lock (_sync)
        {
            return new Dictionary<string, DateTime>(_lastCollectedByAsset);
        }
    }
}
