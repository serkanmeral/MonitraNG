using System.Collections.Concurrent;
using MngDocument.Application.Interfaces;

namespace MngDocument.Infrastructure.Services;

public sealed class InMemoryWopiSessionStore : IWopiSessionStore
{
    private sealed record Entry(WopiSession Session, DateTime ExpiresAt);

    private readonly ConcurrentDictionary<string, Entry> _sessions = new();

    public string CreateSession(WopiSession session, TimeSpan ttl)
    {
        var token = Guid.NewGuid().ToString("N");
        _sessions[token] = new Entry(session, DateTime.UtcNow.Add(ttl));
        return token;
    }

    public WopiSession? GetSession(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return null;

        if (!_sessions.TryGetValue(accessToken, out var entry))
            return null;

        if (entry.ExpiresAt < DateTime.UtcNow)
        {
            _sessions.TryRemove(accessToken, out _);
            return null;
        }

        return entry.Session;
    }

    public void BumpVersion(string accessToken, string newVersion)
    {
        if (!_sessions.TryGetValue(accessToken, out var entry))
            return;

        entry.Session.Version = newVersion;
    }
}
