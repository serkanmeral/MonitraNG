using System.Collections.Concurrent;

namespace MngOperations.Application.Diagnostics;

/// <summary>
/// GEÇİCİ ölçüm yardımcısı (perf/oc-optimization). İstek başına (scoped) downstream çağrı
/// sayısı/süresi: Data Gateway + Keeper. Yalnızca <c>MngOperationsSettings.PerfDiagnostics</c>
/// açıkken loglanır; kayıt maliyeti ihmal edilebilir. Optimizasyon turu bitince kaldırılabilir.
/// </summary>
public sealed class OcCallStats
{
    private int _dgCount;
    private long _dgMs;
    private int _keeperCount;
    private long _keeperMs;
    private readonly ConcurrentDictionary<string, int> _ops = new(StringComparer.Ordinal);

    public int DgCount => Volatile.Read(ref _dgCount);
    public long DgMs => Interlocked.Read(ref _dgMs);
    public int KeeperCount => Volatile.Read(ref _keeperCount);
    public long KeeperMs => Interlocked.Read(ref _keeperMs);

    public void RecordDg(string op, long ms)
    {
        Interlocked.Increment(ref _dgCount);
        Interlocked.Add(ref _dgMs, ms);
        Bump($"dg:{op}", 1);
    }

    public void RecordKeeper(int count, long ms)
    {
        if (count <= 0) return;
        Interlocked.Add(ref _keeperCount, count);
        Interlocked.Add(ref _keeperMs, ms);
        Bump("keeper:getUser", count);
    }

    private void Bump(string key, int count) =>
        _ops.AddOrUpdate(key, count, (_, v) => v + count);

    /// <summary>En çok çağrılan op'lar (azalan), log satırı için kısa özet.</summary>
    public string OpSummary() =>
        _ops.Count == 0
            ? "-"
            : string.Join(", ", _ops.OrderByDescending(k => k.Value).Select(k => $"{k.Key}={k.Value}"));
}
