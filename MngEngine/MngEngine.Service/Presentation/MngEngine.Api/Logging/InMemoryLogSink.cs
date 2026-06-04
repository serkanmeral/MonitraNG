using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace MngEngine.Api.Logging;

/// <summary>
/// Son N log kaydını bellekte tutar. Log ekranı API'si için kullanılır.
/// </summary>
public class InMemoryLogSink : ILogEventSink
{
    private const int DefaultCapacity = 1000;
    private readonly int _capacity;
    private readonly ConcurrentQueue<LogEntry> _entries = new();
    private int _count;

    public InMemoryLogSink(int capacity = DefaultCapacity)
    {
        _capacity = capacity;
    }

    public void Emit(LogEvent logEvent)
    {
        var entry = new LogEntry
        {
            Timestamp = logEvent.Timestamp.UtcDateTime,
            Level = logEvent.Level.ToString(),
            Message = logEvent.RenderMessage(),
            Exception = logEvent.Exception?.ToString()
        };

        _entries.Enqueue(entry);
        if (Interlocked.Increment(ref _count) > _capacity && _entries.TryDequeue(out _))
            Interlocked.Decrement(ref _count);
    }

    public IReadOnlyList<LogEntry> GetRecent(int? take = null)
    {
        var list = _entries.ToArray();
        var n = take.HasValue ? Math.Min(take.Value, list.Length) : list.Length;
        return list.TakeLast(n).ToList();
    }

    public void Clear()
    {
        while (_entries.TryDequeue(out _)) { }
        Interlocked.Exchange(ref _count, 0);
    }
}

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Exception { get; set; }
}
