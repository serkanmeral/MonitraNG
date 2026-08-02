using System.Threading.Channels;
using MngLogCollector.Application.Abstractions.Discovery;

namespace MngLogCollector.Application.Services.Discovery;

public sealed class DiscoveryScanQueue : IDiscoveryScanQueue
{
    private readonly Channel<(string DatabaseName, string RunId)> _channel =
        Channel.CreateUnbounded<(string, string)>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public ValueTask EnqueueAsync(string databaseName, string runId, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync((databaseName, runId), ct);

    public IAsyncEnumerable<(string DatabaseName, string RunId)> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}
