using System.Collections.Concurrent;

namespace MngAlarm.Infrastructure.State;

public interface ICorrelationWindowStore
{
    int RecordAndCount(string storeKey, DateTime eventTime, TimeSpan window);

    int GetCount(string storeKey, DateTime now, TimeSpan window);

    IEnumerable<(string StoreKey, int Count)> EnumerateRule(string domainName, string ruleId, TimeSpan window, DateTime now);

    int PruneExpired(string domainName, DateTime now);
}

public interface IObservationActivityStore
{
    void Record(string activityKey, DateTime timestamp);

    DateTime? GetLastSeen(string activityKey);

    IEnumerable<string> EnumerateKeys(string domainName, string ruleId);
}

public sealed class InMemoryCorrelationWindowStore : ICorrelationWindowStore
{
    private readonly ConcurrentDictionary<string, List<DateTime>> _windows = new();

    public int RecordAndCount(string storeKey, DateTime eventTime, TimeSpan window)
    {
        var list = _windows.GetOrAdd(storeKey, _ => []);
        lock (list)
        {
            list.Add(eventTime);
            PruneList(list, eventTime, window);
            return list.Count;
        }
    }

    public int GetCount(string storeKey, DateTime now, TimeSpan window)
    {
        if (!_windows.TryGetValue(storeKey, out var list))
            return 0;

        lock (list)
        {
            PruneList(list, now, window);
            return list.Count;
        }
    }

    public IEnumerable<(string StoreKey, int Count)> EnumerateRule(
        string domainName,
        string ruleId,
        TimeSpan window,
        DateTime now)
    {
        var prefix = $"{domainName}:{ruleId}:";
        foreach (var key in _windows.Keys)
        {
            if (!key.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            yield return (key, GetCount(key, now, window));
        }
    }

    public int PruneExpired(string domainName, DateTime now)
    {
        var removed = 0;
        foreach (var key in _windows.Keys.ToList())
        {
            if (!key.StartsWith($"{domainName}:", StringComparison.Ordinal))
                continue;

            if (_windows.TryGetValue(key, out var list))
            {
                lock (list)
                {
                    if (list.Count == 0 || list.Max() < now.AddHours(-24))
                    {
                        if (_windows.TryRemove(key, out _))
                            removed++;
                    }
                }
            }
        }

        return removed;
    }

    private static void PruneList(List<DateTime> list, DateTime now, TimeSpan window)
    {
        var cutoff = now - window;
        list.RemoveAll(t => t < cutoff);
    }
}

public sealed class InMemoryObservationActivityStore : IObservationActivityStore
{
    private readonly ConcurrentDictionary<string, DateTime> _lastSeen = new();

    public void Record(string activityKey, DateTime timestamp) =>
        _lastSeen.AddOrUpdate(activityKey, timestamp, (_, existing) => timestamp > existing ? timestamp : existing);

    public DateTime? GetLastSeen(string activityKey) =>
        _lastSeen.TryGetValue(activityKey, out var ts) ? ts : null;

    public IEnumerable<string> EnumerateKeys(string domainName, string ruleId)
    {
        var prefix = $"{domainName}:{ruleId}:";
        foreach (var key in _lastSeen.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.Ordinal))
                yield return key;
        }
    }
}
