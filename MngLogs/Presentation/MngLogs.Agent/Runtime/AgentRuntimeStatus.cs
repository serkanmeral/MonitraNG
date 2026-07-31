using System.Collections.Concurrent;
using System.Globalization;
using MngLogs.Agent.Contracts;
using MngLogs.Agent.Metrics;

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
    private HostInventorySnapshot? _hostInventory;

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

    /// <summary>Successful poll with no new events — clear stale access errors.</summary>
    public void MarkEventLogIdle()
    {
        LastEventLogUtc = DateTime.UtcNow;
        LastEventLogError = null;
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

    public void MarkServiceWatchIdle()
    {
        LastServiceWatchUtc = DateTime.UtcNow;
        LastServiceWatchError = null;
    }

    public void MarkServiceWatchError(string error)
    {
        LastServiceWatchError = error;
        Note($"Service watch error: {error}");
    }

    public void UpdateServiceWatchSnapshot(
        string kind,
        string name,
        string health,
        string? statusText,
        string? displayName,
        bool restartAllowed,
        int? instanceCount = null,
        int? minCount = null)
    {
        LastServiceWatchUtc = DateTime.UtcNow;
        var key = $"{kind}:{name}";
        _serviceWatch.TryGetValue(key, out var prev);
        _serviceWatch[key] = new ServiceWatchSnapshotItem
        {
            Kind = kind,
            Name = name,
            DisplayName = displayName,
            Health = health,
            StatusText = statusText,
            RestartAllowed = restartAllowed,
            InstanceCount = instanceCount,
            MinCount = minCount,
            UpdatedAtUtc = DateTime.UtcNow,
            LastOsEventId = prev?.LastOsEventId,
            LastOsEventAtUtc = prev?.LastOsEventAtUtc,
            LastOsEventAction = prev?.LastOsEventAction,
            LastOsEventMessage = prev?.LastOsEventMessage,
            LastRestartAtUtc = prev?.LastRestartAtUtc,
            LastRestartOk = prev?.LastRestartOk,
            LastRestartError = prev?.LastRestartError,
            RestartAttemptCount = prev?.RestartAttemptCount ?? 0
        };
    }

    /// <summary>
    /// Drops snapshot rows whose keys are not in the current policy (rename/delete/disable).
    /// Returns how many entries were removed.
    /// </summary>
    public int PruneServiceWatchSnapshot(IEnumerable<string> activeKeys)
    {
        var keep = new HashSet<string>(activeKeys, StringComparer.OrdinalIgnoreCase);
        var removed = 0;
        foreach (var key in _serviceWatch.Keys)
        {
            if (keep.Contains(key))
                continue;
            if (_serviceWatch.TryRemove(key, out _))
                removed++;
        }

        if (removed > 0)
            LastServiceWatchUtc = DateTime.UtcNow;

        return removed;
    }

    public void NoteOsServiceEvent(
        string serviceLabel,
        int eventId,
        string? action,
        string? message,
        DateTime atUtc)
    {
        if (string.IsNullOrWhiteSpace(serviceLabel))
            return;

        foreach (var kv in _serviceWatch.ToArray())
        {
            var item = kv.Value;
            if (!string.Equals(item.Kind, "service", StringComparison.OrdinalIgnoreCase))
                continue;

            var match =
                string.Equals(item.Name, serviceLabel, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(item.DisplayName) &&
                 string.Equals(item.DisplayName, serviceLabel, StringComparison.OrdinalIgnoreCase));
            if (!match)
                continue;

            _serviceWatch[kv.Key] = new ServiceWatchSnapshotItem
            {
                Kind = item.Kind,
                Name = item.Name,
                DisplayName = item.DisplayName,
                Health = item.Health,
                StatusText = item.StatusText,
                RestartAllowed = item.RestartAllowed,
                InstanceCount = item.InstanceCount,
                MinCount = item.MinCount,
                UpdatedAtUtc = item.UpdatedAtUtc,
                LastOsEventId = eventId,
                LastOsEventAtUtc = atUtc,
                LastOsEventAction = action,
                LastOsEventMessage = Truncate(message, 160),
                LastRestartAtUtc = item.LastRestartAtUtc,
                LastRestartOk = item.LastRestartOk,
                LastRestartError = item.LastRestartError,
                RestartAttemptCount = item.RestartAttemptCount
            };
        }
    }

    public void NoteServiceRestart(string serviceName, bool ok, string? error, int attemptCount) =>
        NoteWatchRestart("service", serviceName, ok, error, attemptCount);

    public void NoteWatchRestart(string kind, string name, bool ok, string? error, int attemptCount)
    {
        var key = $"{kind}:{name}";
        if (!_serviceWatch.TryGetValue(key, out var item))
            return;

        _serviceWatch[key] = new ServiceWatchSnapshotItem
        {
            Kind = item.Kind,
            Name = item.Name,
            DisplayName = item.DisplayName,
            Health = item.Health,
            StatusText = item.StatusText,
            RestartAllowed = item.RestartAllowed,
            InstanceCount = item.InstanceCount,
            MinCount = item.MinCount,
            UpdatedAtUtc = item.UpdatedAtUtc,
            LastOsEventId = item.LastOsEventId,
            LastOsEventAtUtc = item.LastOsEventAtUtc,
            LastOsEventAction = item.LastOsEventAction,
            LastOsEventMessage = item.LastOsEventMessage,
            LastRestartAtUtc = DateTime.UtcNow,
            LastRestartOk = ok,
            LastRestartError = error,
            RestartAttemptCount = attemptCount
        };
    }

    public void ResetServiceRestartAttempts(string serviceName) =>
        ResetWatchRestartAttempts("service", serviceName);

    public void ResetWatchRestartAttempts(string kind, string name)
    {
        var key = $"{kind}:{name}";
        if (!_serviceWatch.TryGetValue(key, out var item))
            return;
        if (item.RestartAttemptCount == 0)
            return;

        _serviceWatch[key] = new ServiceWatchSnapshotItem
        {
            Kind = item.Kind,
            Name = item.Name,
            DisplayName = item.DisplayName,
            Health = item.Health,
            StatusText = item.StatusText,
            RestartAllowed = item.RestartAllowed,
            InstanceCount = item.InstanceCount,
            MinCount = item.MinCount,
            UpdatedAtUtc = item.UpdatedAtUtc,
            LastOsEventId = item.LastOsEventId,
            LastOsEventAtUtc = item.LastOsEventAtUtc,
            LastOsEventAction = item.LastOsEventAction,
            LastOsEventMessage = item.LastOsEventMessage,
            LastRestartAtUtc = item.LastRestartAtUtc,
            LastRestartOk = item.LastRestartOk,
            LastRestartError = item.LastRestartError,
            RestartAttemptCount = 0
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
        TryNoteOsServiceEvent(item, entry);
    }

    private void TryNoteOsServiceEvent(IngestEventItem item, RecentEventEntry entry)
    {
        if (!string.Equals(item.Source, "windows-eventlog", StringComparison.OrdinalIgnoreCase))
            return;
        if (item.Fields is null)
            return;
        if (!item.Fields.TryGetValue("serviceName", out var sn) || sn is null)
            return;

        var serviceLabel = Convert.ToString(sn, CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(serviceLabel))
            return;

        var eventId = entry.EventId ?? 0;
        if (eventId == 0 &&
            item.Fields.TryGetValue("eventId", out var eid) &&
            eid != null &&
            int.TryParse(Convert.ToString(eid, CultureInfo.InvariantCulture), NumberStyles.Any,
                CultureInfo.InvariantCulture, out var parsed))
        {
            eventId = parsed;
        }

        string? action = null;
        if (item.Fields.TryGetValue("event.action", out var a) && a != null)
            action = Convert.ToString(a, CultureInfo.InvariantCulture);
        if (string.IsNullOrWhiteSpace(action))
            action = entry.Action;

        NoteOsServiceEvent(
            serviceLabel,
            eventId,
            action,
            entry.Message,
            entry.AtUtc == default ? DateTime.UtcNow : entry.AtUtc);
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value))
            return value;
        return value.Length <= max ? value : value[..max];
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

    public void UpdateHostInventory(HostInventorySnapshot snapshot) =>
        _hostInventory = snapshot;

    public HostInventorySnapshot? HostInventory() => _hostInventory;

    public void ClearRecentEvents()
    {
        while (_producedEvents.TryDequeue(out _)) { }
        while (_shippedEvents.TryDequeue(out _)) { }
        _latestMetrics.Clear();
        _topProcesses = null;
        _hostInventory = null;
    }

    private void RememberMetric(RecentEventEntry entry)
    {
        if (!string.Equals(entry.Source, "metric", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(entry.MetricName) ||
            entry.MetricValue is null)
            return;

        // Top-process / watch inventory summaries are list payloads; dedicated UI, not scalar tiles.
        if (entry.MetricName.StartsWith("process.top", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entry.MetricName, "watch.inventory", StringComparison.OrdinalIgnoreCase))
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
        string? channel = null;
        string? package = null;
        string? provider = null;
        int? eventId = null;
        long? recordId = null;
        Dictionary<string, object?>? fieldsCopy = null;

        if (item.Fields != null)
        {
            fieldsCopy = new Dictionary<string, object?>(item.Fields, StringComparer.OrdinalIgnoreCase);

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
            else if (item.Fields.TryGetValue("processName", out var pn) && pn != null)
                detail = Convert.ToString(pn, CultureInfo.InvariantCulture);
            else if (item.Fields.TryGetValue("package", out var pkg) && pkg != null)
                detail = Convert.ToString(pkg, CultureInfo.InvariantCulture);

            if (item.Fields.TryGetValue("channel", out var ch2) && ch2 != null)
                channel = Convert.ToString(ch2, CultureInfo.InvariantCulture);
            if (item.Fields.TryGetValue("package", out var pkg2) && pkg2 != null)
                package = Convert.ToString(pkg2, CultureInfo.InvariantCulture);
            if (item.Fields.TryGetValue("provider", out var prov) && prov != null)
                provider = Convert.ToString(prov, CultureInfo.InvariantCulture);
            if (item.Fields.TryGetValue("eventId", out var eid) && eid != null &&
                int.TryParse(Convert.ToString(eid, CultureInfo.InvariantCulture), NumberStyles.Any,
                    CultureInfo.InvariantCulture, out var eidParsed))
                eventId = eidParsed;
            if (item.Fields.TryGetValue("recordId", out var rid) && rid != null &&
                long.TryParse(Convert.ToString(rid, CultureInfo.InvariantCulture), NumberStyles.Any,
                    CultureInfo.InvariantCulture, out var ridParsed))
                recordId = ridParsed;

            if (item.Fields.TryGetValue("event.action", out var a) && a != null)
                action = Convert.ToString(a, CultureInfo.InvariantCulture);
            else if (string.IsNullOrWhiteSpace(action) && metricName != null)
                action = metricName;
            else if (string.IsNullOrWhiteSpace(action) && detail != null)
                action = detail;
        }

        string? rawJson = null;
        if (item.Raw is { } rawEl)
        {
            try { rawJson = rawEl.GetRawText(); }
            catch { /* ignore */ }
        }
        else if (fieldsCopy is { Count: > 0 })
        {
            try
            {
                rawJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    id = item.Id,
                    timestampUtc = item.TimestampUtc,
                    source = item.Source,
                    sourceProduct = item.SourceProduct,
                    severity = item.Severity,
                    message = item.Message,
                    fields = fieldsCopy
                });
            }
            catch { /* ignore */ }
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
            Detail = detail,
            Id = item.Id,
            Channel = channel,
            Package = package ?? item.SourceProduct,
            EventId = eventId,
            RecordId = recordId,
            Provider = provider,
            RawJson = rawJson,
            Fields = fieldsCopy
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
