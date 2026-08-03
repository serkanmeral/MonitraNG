using MngLogs.Agent.Contracts;
using MngLogs.Agent.Runtime;

namespace MngLogs.Agent.Queue;

/// <summary>Decorates disk queue to feed local UI recent-event buffers.</summary>
public sealed class ObservingOutboundQueue : IOutboundQueue
{
    private readonly IOutboundQueue _inner;
    private readonly AgentRuntimeStatus _status;

    public ObservingOutboundQueue(IOutboundQueue inner, AgentRuntimeStatus status)
    {
        _inner = inner;
        _status = status;
    }

    public int PendingCount => _inner.PendingCount;

    public async Task EnqueueAsync(IngestEventItem item, CancellationToken cancellationToken = default)
    {
        await _inner.EnqueueAsync(item, cancellationToken);
        _status.RecordProduced(item);
    }

    public Task<IReadOnlyList<(string FilePath, IngestEventItem Item)>> DequeueBatchAsync(
        int maxCount,
        CancellationToken cancellationToken = default) =>
        _inner.DequeueBatchAsync(maxCount, cancellationToken);

    public void Complete(IEnumerable<string> filePaths) => _inner.Complete(filePaths);

    public IReadOnlyList<(string FileName, IngestEventItem Item)> Peek(int maxCount) =>
        _inner.Peek(maxCount);
}
