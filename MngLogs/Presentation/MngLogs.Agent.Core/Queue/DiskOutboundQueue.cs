using System.Text.Json;
using MngLogs.Agent.Contracts;

namespace MngLogs.Agent.Queue;

public interface IOutboundQueue
{
    Task EnqueueAsync(IngestEventItem item, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<(string FilePath, IngestEventItem Item)>> DequeueBatchAsync(
        int maxCount,
        CancellationToken cancellationToken = default);
    void Complete(IEnumerable<string> filePaths);
    int PendingCount { get; }

    /// <summary>Read pending items without removing them (local UI).</summary>
    IReadOnlyList<(string FileName, IngestEventItem Item)> Peek(int maxCount);
}

/// <summary>Simple one-file-per-event disk queue under DataDirectory/queue.</summary>
public sealed class DiskOutboundQueue : IOutboundQueue
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _queueDir;
    private readonly object _gate = new();

    public DiskOutboundQueue(string dataDirectory)
    {
        _queueDir = Path.Combine(dataDirectory, "queue");
        Directory.CreateDirectory(_queueDir);
    }

    public int PendingCount
    {
        get
        {
            lock (_gate)
                return Directory.Exists(_queueDir)
                    ? Directory.GetFiles(_queueDir, "*.json").Length
                    : 0;
        }
    }

    public async Task EnqueueAsync(IngestEventItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        var name = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}.json";
        var path = Path.Combine(_queueDir, name);
        var json = JsonSerializer.Serialize(item, JsonOptions);
        var tmp = path + ".tmp";
        await File.WriteAllTextAsync(tmp, json, cancellationToken);
        File.Move(tmp, path, overwrite: true);
    }

    public Task<IReadOnlyList<(string FilePath, IngestEventItem Item)>> DequeueBatchAsync(
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        maxCount = Math.Max(1, maxCount);
        var result = new List<(string, IngestEventItem)>();

        lock (_gate)
        {
            if (!Directory.Exists(_queueDir))
                return Task.FromResult<IReadOnlyList<(string, IngestEventItem)>>(result);

            foreach (var file in Directory.EnumerateFiles(_queueDir, "*.json").OrderBy(f => f).Take(maxCount))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var json = File.ReadAllText(file);
                    var item = JsonSerializer.Deserialize<IngestEventItem>(json, JsonOptions);
                    if (item != null)
                        result.Add((file, item));
                    else
                        TryDelete(file);
                }
                catch
                {
                    TryDelete(file);
                }
            }
        }

        return Task.FromResult<IReadOnlyList<(string, IngestEventItem)>>(result);
    }

    public void Complete(IEnumerable<string> filePaths)
    {
        foreach (var path in filePaths)
            TryDelete(path);
    }

    public IReadOnlyList<(string FileName, IngestEventItem Item)> Peek(int maxCount)
    {
        maxCount = Math.Clamp(maxCount, 1, 200);
        var result = new List<(string, IngestEventItem)>();

        lock (_gate)
        {
            if (!Directory.Exists(_queueDir))
                return result;

            foreach (var file in Directory.EnumerateFiles(_queueDir, "*.json").OrderBy(f => f).Take(maxCount))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var item = JsonSerializer.Deserialize<IngestEventItem>(json, JsonOptions);
                    if (item != null)
                        result.Add((Path.GetFileName(file), item));
                }
                catch
                {
                    // skip corrupt for peek
                }
            }
        }

        return result;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // leave for retry / manual cleanup
        }
    }
}
