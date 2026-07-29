using System.Collections.Concurrent;
using System.Globalization;
using MngLogs.Agent.Contracts;

namespace MngLogs.Agent.Runtime;

public sealed class AgentRuntimeStatus
{
    private readonly ConcurrentQueue<string> _recent = new();
    private readonly ConcurrentQueue<RecentEventEntry> _producedEvents = new();
    private readonly ConcurrentQueue<RecentEventEntry> _shippedEvents = new();
    private readonly ConcurrentDictionary<string, ServiceWatchSnapshotItem> _serviceWatch =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, LatestMetricItem> _latestMetrics =
        new(StringComparer.OrdinalIgnoreCase);
    private TopProcessSnapshot? _topProcesses;

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

    public void RecordProduced(IngestEventItem item)
    {
        var entry = ToEntry(item, "produced");
        EnqueueEvent(_producedEvents, entry);
        RememberMetric(entry);
    }

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

    public IReadOnlyList<LatestMetricItem> LatestMetrics() =>
        _latestMetrics.Values
            .OrderBy(m => MetricSortKey(m.Name))
            .ThenBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public IReadOnlyList<RecentEventEntry> LatestLogEvents(int take = 15) =>
        TakeLast(_producedEvents, 80)
            .Where(e => !string.Equals(e.Source, "metric", StringComparison.OrdinalIgnoreCase))
            .Reverse()
            .Take(Math.Clamp(take, 1, 50))
            .ToArray();

    public IReadOnlyList<ServiceWatchSnapshotItem> ServiceWatchSnapshot() =>
        _serviceWatch.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();

    public void UpdateTopProcesses(TopProcessSnapshot snapshot) =>
        _topProcesses = snapshot;

    public TopProcessSnapshot? TopProcesses() => _topProcesses;

    public void ClearRecentEvents()
    {
        while (_producedEvents.TryDequeue(out _)) { }
        while (_shippedEvents.TryDequeue(out _)) { }
        _latestMetrics.Clear();
        _topProcesses = null;
    }

    private void RememberMetric(RecentEventEntry entry)
    {
        if (!string.Equals(entry.Source, "metric", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(entry.MetricName) ||
            entry.MetricValue is null)
            return;

        // Top-process summaries are list payloads; dedicated UI card, not scalar tiles.
        if (entry.MetricName.StartsWith("process.top", StringComparison.OrdinalIgnoreCase))
            return;

        var key = string.IsNullOrWhiteSpace(entry.Detail)
            ? entry.MetricName!
            : $"{entry.MetricName}|{entry.Detail}";

        _latestMetrics[key] = new LatestMetricItem
        {
            Name = entry.MetricName!,
            Value = entry.MetricValue.Value,
            Message = entry.Message,
            Detail = entry.Detail,
            AtUtc = entry.AtUtc
        };
    }

    private static RecentEventEntry ToEntry(IngestEventItem item, string direction)
    {
        string? action = item.Message;
        string? metricName = null;
        double? metricValue = null;
        string? detail = null;

        if (item.Fields != null)
        {
            if (item.Fields.TryGetValue("metric", out var m) && m != null)
                metricName = Convert.ToString(m, CultureInfo.InvariantCulture);
            if (item.Fields.TryGetValue("value", out var v) && v != null &&
                double.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Any,
                    CultureInfo.InvariantCulture, out var parsed))
                metricValue = parsed;

            if (item.Fields.TryGetValue("volume", out var vol) && vol != null)
                detail = Convert.ToString(vol, CultureInfo.InvariantCulture);
            else if (item.Fields.TryGetValue("channel", out var ch) && ch != null)
                detail = Convert.ToString(ch, CultureInfo.InvariantCulture);
            else if (item.Fields.TryGetValue("serviceName", out var sn) && sn != null)
                detail = Convert.ToString(sn, CultureInfo.InvariantCulture);
            else if (item.Fields.TryGetValue("package", out var pkg) && pkg != null)
                detail = Convert.ToString(pkg, CultureInfo.InvariantCulture);

            if (string.IsNullOrWhiteSpace(action) &&
                item.Fields.TryGetValue("event.action", out var a) && a != null)
                action = Convert.ToString(a, CultureInfo.InvariantCulture);
            else if (string.IsNullOrWhiteSpace(action) && metricName != null)
                action = metricName;
            else if (string.IsNullOrWhiteSpace(action) && detail != null)
                action = detail;
        }

        return new RecentEventEntry
        {
            AtUtc = item.TimestampUtc ?? DateTime.UtcNow,
            Direction = direction,
            Source = item.Source,
            Severity = item.Severity,
            Message = item.Message,
            Action = action,
            MetricName = metricName,
            MetricValue = metricValue,
            Detail = detail
        };
    }

    private static int MetricSortKey(string name) => name switch
    {
        "up" => 0,
        "cpu.percent" => 1,
        "memory.available_bytes" => 2,
        "memory.process_working_set_bytes" => 3,
        _ when name.StartsWith("disk.free", StringComparison.OrdinalIgnoreCase) => 4,
        _ when name.StartsWith("disk.total", StringComparison.OrdinalIgnoreCase) => 5,
        _ => 9
    };

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
