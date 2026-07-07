using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using MngDocument.Application.Configuration;
using MngDocument.Application.Interfaces;

namespace MngDocument.Infrastructure.Services;

public sealed class InMemoryWopiSessionStore : IWopiSessionStore
{
    private sealed record Entry(WopiSession Session, DateTime ExpiresAt, DateTime LastSeenAt);

    private readonly ConcurrentDictionary<string, Entry> _sessions = new();
    private readonly MngDocumentSettings _settings;

    public InMemoryWopiSessionStore(IOptions<MngDocumentSettings> settings)
    {
        _settings = settings.Value;
    }

    public string CreateSession(WopiSession session, TimeSpan ttl)
    {
        var token = Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        session.LastSeenAt = now;
        _sessions[token] = new Entry(session, now.Add(ttl), now);
        return token;
    }

    public WopiSession? GetSession(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return null;

        if (!_sessions.TryGetValue(accessToken, out var entry))
            return null;

        var now = DateTime.UtcNow;
        var idleTimeout = GetIdleTimeout();

        if (entry.ExpiresAt < now || now - entry.LastSeenAt > idleTimeout)
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
        Touch(accessToken);
    }

    public void Touch(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return;

        if (!_sessions.TryGetValue(accessToken, out var entry))
            return;

        var now = DateTime.UtcNow;
        if (entry.ExpiresAt < now)
        {
            _sessions.TryRemove(accessToken, out _);
            return;
        }

        entry.Session.LastSeenAt = now;
        _sessions[accessToken] = entry with { LastSeenAt = now };
    }

    public bool Revoke(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return false;

        return _sessions.TryRemove(accessToken, out _);
    }

    public int RevokeByUser(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return 0;

        var keys = _sessions
            .Where(kv => string.Equals(kv.Value.Session.UserId, userId, StringComparison.Ordinal))
            .Select(kv => kv.Key)
            .ToList();

        var removed = 0;
        foreach (var key in keys)
        {
            if (_sessions.TryRemove(key, out _))
                removed++;
        }

        return removed;
    }

    public IReadOnlyList<WopiActiveSession> ListActive(TimeSpan idleTimeout)
    {
        PurgeExpired(idleTimeout);
        var now = DateTime.UtcNow;

        return _sessions
            .Where(kv => kv.Value.ExpiresAt >= now && now - kv.Value.LastSeenAt <= idleTimeout)
            .Select(kv => new WopiActiveSession
            {
                AccessToken = kv.Key,
                Session = kv.Value.Session,
                ExpiresAt = kv.Value.ExpiresAt,
                LastSeenAt = kv.Value.LastSeenAt
            })
            .ToList();
    }

    public void PurgeExpired(TimeSpan idleTimeout)
    {
        var now = DateTime.UtcNow;
        foreach (var kv in _sessions)
        {
            var expired = kv.Value.ExpiresAt < now || now - kv.Value.LastSeenAt > idleTimeout;
            if (expired)
                _sessions.TryRemove(kv.Key, out _);
        }
    }

    private TimeSpan GetIdleTimeout()
    {
        var minutes = Math.Clamp(_settings.EditorLimits.IdleTimeoutMinutes, 5, 1440);
        return TimeSpan.FromMinutes(minutes);
    }
}
