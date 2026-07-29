using System.Collections.Concurrent;
using MngLogs.Agent.Contracts;

namespace MngLogs.Agent.Runtime;

public sealed class AgentRuntimeStatus
{
    private readonly ConcurrentQueue<string> _recent = new();
    private readonly ConcurrentQueue<RecentEventEntry> _producedEvents = new();
    private readonly ConcurrentQueue<RecentEventEntry> _shippedEvents = new();
    private readonly ConcurrentDictionary<string, ServiceWatchSnapshotItem> _serviceWatch =
        new(StringComparer.OrdinalIgnoreCase);

    public DateTime StartedAtUtc { get; } = DateTime.UtcNow;
    public DateTime? LastHeartbeatUtc { get; private set; }
    public DateTime? LastEventLogUtc { get; private set; }
    public DateTime? LastShipUtc { get; private set; }
    public DateTime? LastShipSuccessUtc { get; private set; }
    public string? LastShipError { get; private set; }
    public string? LastEventLogError { get; private set; }
    public long HeartbeatsProduced { get; private set; }
    public long MetricEventsProduced { get; private set; }
    public long EventLogEventsProduced { get; private set; }
    public long ServiceWatchEventsProduced { get; private set; }
    public long EventsShipped { get; private set; }
    public bool? CollectorHealthy { get; private set; }
    public DateTime? LastServiceWatchUtc { get; private set; }
    public string? LastServiceWatchError { get; private set; }

    public void MarkHeartbeat(int metricEventCount = 1)
    {
        LastHeartbeatUtc = DateTime.UtcNow;
        HeartbeatsProduced++;
        MetricEventsProduced += metricEventCount;
        Note($"Metrics enqueued ({metricEventCount})");
    }

    public void MarkEventLogCollected(int count)
    {
        LastEventLogUtc = DateTime.UtcNow;
        LastEventLogError = null;
        EventLogEventsProduced += count;
        Note($"Event Log enqueued {count}");
    }

    public void MarkEventLogError(string error)
    {
        LastEventLogError = error;
        Note($"Event Log error: {error}");
    }

    public void MarkServiceWatchEvents(int count)
    {
        LastServiceWatchUtc = DateTime.UtcNow;
        LastServiceWatchError = null;
        ServiceWatchEventsProduced += count;
        Note($"Service watch enqueued {count}");
    }

    public void MarkServiceWatchError(string error)
    {
        LastServiceWatchError = error;
        Note($"Service watch error: {error}");
    }

    public void UpdateServiceWatchSnapshot(
        string name,
        string health,
        string? statusText,
        string? displayName,
        bool restartAllowed)
    {
        LastServiceWatchUtc = DateTime.UtcNow;
        _serviceWatch[name] = new ServiceWatchSnapshotItem
        {
            Name = name,
            DisplayName = displayName,
            Health = health,
            StatusText = statusText,
            RestartAllowed = restartAllowed,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    public void MarkShipAttempt(int count, bool success, string? error = null)
    {
        LastShipUtc = DateTime.UtcNow;
        if (success)
        {
            LastShipSuccessUtc = LastShipUtc;
            LastShipError = null;
            EventsShipped += count;
            Note($"Shipped {count} event(s)");
        }
        else
        {
            LastShipError = error ?? "ship failed";
            Note($"Ship failed: {LastShipError}");
        }
    }

    public void MarkCollectorHealth(bool healthy)
    {
        CollectorHealthy = healthy;
    }

    public void RecordProduced(IngestEventItem item) =>
        EnqueueEvent(_producedEvents, ToEntry(item, "produced"));

    public void RecordShipped(IEnumerable<IngestEventItem> items)
    {
        foreach (var item in items)
            EnqueueEvent(_shippedEvents, ToEntry(item, "shipped"));
    }

    public IReadOnlyList<string> RecentNotes() => _recent.ToArray();

    public IReadOnlyList<RecentEventEntry> RecentProduced(int take = 100) =>
        TakeLast(_producedEvents, take);

    public IReadOnlyList<RecentEventEntry> RecentShipped(int take = 100) =>
        TakeLast(_shippedEvents, take);

    public IReadOnlyList<ServiceWatchSnapshotItem> ServiceWatchSnapshot() =>
        _serviceWatch.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();

    public void ClearRecentEvents()
    {
        while (_producedEvents.TryDequeue(out _)) { }
        while (_shippedEvents.TryDequeue(out _)) { }
    }

    private static RecentEventEntry ToEntry(IngestEventItem item, string direction)
    {
        string? action = item.Message;
        if (item.Fields != null)
        {
            if (string.IsNullOrWhiteSpace(action) &&
                item.Fields.TryGetValue("event.action", out var a) && a != null)
                action = a.ToString();
            else if (string.IsNullOrWhiteSpace(action) &&
                     item.Fields.TryGetValue("metric", out var m) && m != null)
                action = m.ToString();
            else if (string.IsNullOrWhiteSpace(action) &&
                     item.Fields.TryGetValue("serviceName", out var s) && s != null)
                action = s.ToString();
        }

        return new RecentEventEntry
        {
            AtUtc = item.TimestampUtc ?? DateTime.UtcNow,
            Direction = direction,
            Source = item.Source,
            Severity = item.Severity,
            Message = item.Message,
            Action = action
        };
    }

    private static void EnqueueEvent(ConcurrentQueue<RecentEventEntry> queue, RecentEventEntry entry)
    {
        queue.Enqueue(entry);
        while (queue.Count > 200 && queue.TryDequeue(out _)) { }
    }

    private static IReadOnlyList<RecentEventEntry> TakeLast(ConcurrentQueue<RecentEventEntry> queue, int take)
    {
        take = Math.Clamp(take, 1, 200);
        var all = queue.ToArray();
        if (all.Length <= take)
            return all;
        return all.Skip(all.Length - take).ToArray();
    }

    private void Note(string message)
    {
        _recent.Enqueue($"{DateTime.UtcNow:O} {message}");
        while (_recent.Count > 40 && _recent.TryDequeue(out _)) { }
    }
}
