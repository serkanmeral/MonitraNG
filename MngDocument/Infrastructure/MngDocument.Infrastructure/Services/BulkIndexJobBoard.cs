using System.Collections.Concurrent;
using MngDocument.Application.Contracts.Resources;

namespace MngDocument.Infrastructure.Services;

/// <summary>One bulk index job per domain. Closing the dialog does not remove it.</summary>
public sealed class BulkIndexJobBoard
{
    private readonly ConcurrentDictionary<string, BulkIndexJob> _jobs = new(StringComparer.OrdinalIgnoreCase);

    public BulkIndexJob? Find(string domain) =>
        _jobs.TryGetValue(domain, out var job) ? job : null;

    public void Set(string domain, BulkIndexJob job) => _jobs[domain] = job;
}

public sealed class BulkIndexJob
{
    private readonly object _sync = new();
    private readonly List<BulkIndexItemDto> _indexed = new();
    private readonly List<BulkIndexItemDto> _skipped = new();
    private readonly List<BulkIndexItemDto> _failed = new();

    public BulkIndexJob(string folderId, string folderName, int total)
    {
        FolderId = folderId;
        FolderName = folderName;
        Total = total;
        Status = "running";
        Stop = new CancellationTokenSource();
    }

    public string FolderId { get; }
    public string FolderName { get; }
    public int Total { get; }
    public CancellationTokenSource Stop { get; }
    public string Status { get; private set; }
    public string? CurrentName { get; private set; }

    public bool IsActive
    {
        get
        {
            lock (_sync)
                return Status is "running" or "stopping";
        }
    }

    public void SetCurrent(string name)
    {
        lock (_sync)
            CurrentName = name;
    }

    public void AddIndexed(string id, string name)
    {
        lock (_sync)
            _indexed.Add(new BulkIndexItemDto { Id = id, Name = name });
    }

    public void AddSkipped(string id, string name)
    {
        lock (_sync)
            _skipped.Add(new BulkIndexItemDto { Id = id, Name = name });
    }

    public void AddFailed(string id, string name, string error)
    {
        lock (_sync)
            _failed.Add(new BulkIndexItemDto { Id = id, Name = name, Error = error });
    }

    public void RequestStop()
    {
        lock (_sync)
        {
            if (Status != "running")
                return;
            Status = "stopping";
            Stop.Cancel();
        }
    }

    public void Finish(bool stopped)
    {
        lock (_sync)
        {
            Status = stopped ? "stopped" : "done";
            CurrentName = null;
        }
    }

    public BulkIndexJobDto ToDto()
    {
        lock (_sync)
        {
            return new BulkIndexJobDto
            {
                Status = Status,
                FolderId = FolderId,
                FolderName = FolderName,
                CurrentName = CurrentName,
                Total = Total,
                IndexedCount = _indexed.Count,
                SkippedCount = _skipped.Count,
                FailedCount = _failed.Count,
                IndexedItems = _indexed.ToArray(),
                SkippedItems = _skipped.ToArray(),
                FailedItems = _failed.ToArray()
            };
        }
    }
}
