using System.Collections.Concurrent;
using MngNotifier.Application.Configuration;

namespace MngNotifier.Infrastructure.Services;

public sealed class DomainMailSettingsCache
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public bool TryGet(string domainName, out MailSettings settings)
    {
        settings = null!;
        if (!_entries.TryGetValue(domainName, out var entry))
            return false;

        if (entry.ExpiresAtUtc <= DateTime.UtcNow)
        {
            _entries.TryRemove(domainName, out _);
            return false;
        }

        settings = entry.Settings;
        return true;
    }

    public void Set(string domainName, MailSettings settings, TimeSpan ttl)
    {
        _entries[domainName] = new Entry(settings, DateTime.UtcNow.Add(ttl));
    }

    private sealed record Entry(MailSettings Settings, DateTime ExpiresAtUtc);
}
