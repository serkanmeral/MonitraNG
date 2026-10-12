using System.Collections.Concurrent;

namespace MngDocument.Infrastructure.Services;

/// <summary>İndeks ve özet aynı anda bir etki alanında tek iş çalıştırır.</summary>
internal static class DocumentJobGate
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);

    public static SemaphoreSlim For(string? domain)
    {
        var key = string.IsNullOrWhiteSpace(domain) ? "default" : domain.Trim();
        return Gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
    }
}
