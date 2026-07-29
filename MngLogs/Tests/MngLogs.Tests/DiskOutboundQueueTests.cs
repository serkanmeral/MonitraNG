using MngLogs.Agent.Queue;
using MngLogs.Agent.Contracts;

namespace MngLogs.Tests;

public class DiskOutboundQueueTests
{
    [Fact]
    public async Task Enqueue_dequeue_complete_roundtrip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mnglogs-agent-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var queue = new DiskOutboundQueue(dir);
            await queue.EnqueueAsync(new IngestEventItem
            {
                Id = "1",
                Source = "metric",
                Message = "host.up"
            });

            Assert.Equal(1, queue.PendingCount);

            var batch = await queue.DequeueBatchAsync(10);
            Assert.Single(batch);
            Assert.Equal("1", batch[0].Item.Id);

            queue.Complete(batch.Select(b => b.FilePath));
            Assert.Equal(0, queue.PendingCount);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* ignore */ }
        }
    }
}
