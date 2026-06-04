using System.Collections.Concurrent;

namespace MngAlarm.Infrastructure.State;

public sealed class SequenceRuntimeState
{
    public bool Armed { get; set; }
    public DateTime? AnchorTime { get; set; }
}

public interface ISequenceStateStore
{
    SequenceRuntimeState GetOrCreate(string storeKey);

    void Save(string storeKey, SequenceRuntimeState state);

    void Reset(string storeKey);
}

public sealed class InMemorySequenceStateStore : ISequenceStateStore
{
    private readonly ConcurrentDictionary<string, SequenceRuntimeState> _states = new();

    public SequenceRuntimeState GetOrCreate(string storeKey) =>
        _states.GetOrAdd(storeKey, _ => new SequenceRuntimeState());

    public void Save(string storeKey, SequenceRuntimeState state) =>
        _states[storeKey] = state;

    public void Reset(string storeKey) =>
        _states.TryRemove(storeKey, out _);
}
