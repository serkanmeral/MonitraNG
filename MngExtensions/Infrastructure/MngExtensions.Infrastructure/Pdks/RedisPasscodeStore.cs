using System.Globalization;
using MngExtensions.Application.Pdks;
using StackExchange.Redis;

namespace MngExtensions.Infrastructure.Pdks;

public sealed class RedisPasscodeStore : IPasscodeStore
{
    private const string TakeScript = """
        local live = redis.call('GET', KEYS[1])
        if live then
          redis.call('DEL', KEYS[1])
          redis.call('SET', KEYS[2], live, 'EX', ARGV[1])
          return live
        end
        local used = redis.call('GET', KEYS[2])
        if used then
          return 'USED|' .. used
        end
        return false
        """;

    private const string HoldScript = """
        local live = redis.call('GET', KEYS[1])
        if not live then
          if redis.call('GET', KEYS[2]) then
            return 'USED'
          end
          return 'MISSING'
        end
        local door, exp, flag = string.match(live, '^([^|]+)|([^|]+)|?(.*)$')
        if not door or not exp then
          return 'MISSING'
        end
        local expn = tonumber(exp)
        local now = tonumber(ARGV[1])
        if not expn or not now then
          return 'MISSING'
        end
        if flag == '1' then
          if expn <= now then
            return 'EXPIRED'
          end
          return 'HELD|' .. exp
        end
        if expn <= now then
          return 'EXPIRED'
        end
        local nextExp = now + tonumber(ARGV[2])
        local ttl = redis.call('TTL', KEYS[1])
        local need = tonumber(ARGV[3])
        if ttl < need then
          ttl = need
        end
        redis.call('SET', KEYS[1], door .. '|' .. string.format('%.0f', nextExp) .. '|1', 'EX', ttl)
        return 'HELD|' .. string.format('%.0f', nextExp)
        """;

    private readonly IConnectionMultiplexer _redis;

    public RedisPasscodeStore(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task StoreAsync(string nonce, string doorKey, DateTime expiresAtUtc, TimeSpan retention, CancellationToken cancellationToken)
    {
        var ttl = retention < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : retention;
        var unixMs = new DateTimeOffset(DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
        var db = _redis.GetDatabase();
        await db.StringSetAsync(LiveKey(nonce), $"{doorKey}|{unixMs.ToString(CultureInfo.InvariantCulture)}", ttl).WaitAsync(cancellationToken);
    }

    public async Task SetWelcomeAsync(string doorKey, string payload, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var life = ttl < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : ttl;
        var db = _redis.GetDatabase();
        await db.StringSetAsync(WelcomeKey(doorKey), payload, life).WaitAsync(cancellationToken);
    }

    public async Task<string?> GetWelcomeAsync(string doorKey, CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(WelcomeKey(doorKey)).WaitAsync(cancellationToken);
        return value.IsNullOrEmpty ? null : value.ToString();
    }

    public async Task SetBoardAsync(string doorKey, string payload, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var life = ttl < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : ttl;
        var db = _redis.GetDatabase();
        await db.StringSetAsync(BoardKey(doorKey), payload, life).WaitAsync(cancellationToken);
    }

    public async Task<string?> GetBoardAsync(string doorKey, CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(BoardKey(doorKey)).WaitAsync(cancellationToken);
        return value.IsNullOrEmpty ? null : value.ToString();
    }

    public async Task<long> ReadBoardStampAsync(CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(BoardStampKey).WaitAsync(cancellationToken);
        return value.TryParse(out long stamp) ? stamp : 0;
    }

    public async Task AdvanceBoardStampAsync(CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();
        await db.StringIncrementAsync(BoardStampKey).WaitAsync(cancellationToken);
    }

    public async Task<PasscodeHold> HoldAsync(string nonce, DateTime nowUtc, TimeSpan hold, CancellationToken cancellationToken)
    {
        var nowMs = new DateTimeOffset(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
        var holdMs = Math.Max(1, (long)hold.TotalMilliseconds);
        var ttlSeconds = Math.Max(1, (int)Math.Ceiling(hold.TotalSeconds) + 15);
        var db = _redis.GetDatabase();
        var raw = await db.ScriptEvaluateAsync(
            HoldScript,
            new RedisKey[] { LiveKey(nonce), UsedKey(nonce) },
            new RedisValue[] { nowMs, holdMs, ttlSeconds }).WaitAsync(cancellationToken);

        if (raw.IsNull)
            return new PasscodeHold(PasscodeHoldKind.Missing, null);

        var text = raw.ToString();
        if (text == "EXPIRED")
            return new PasscodeHold(PasscodeHoldKind.Expired, null);
        if (text == "USED")
            return new PasscodeHold(PasscodeHoldKind.Used, null);
        if (text.StartsWith("HELD|", StringComparison.Ordinal)
            && long.TryParse(text["HELD|".Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixMs))
            return new PasscodeHold(PasscodeHoldKind.Held, DateTimeOffset.FromUnixTimeMilliseconds(unixMs).UtcDateTime);

        return new PasscodeHold(PasscodeHoldKind.Missing, null);
    }

    public async Task<PasscodePeek> PeekAsync(string nonce, CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(LiveKey(nonce)).WaitAsync(cancellationToken);
        if (value.IsNullOrEmpty)
            return new PasscodePeek(null, null);

        var parsed = ParsePayload(value.ToString());
        return new PasscodePeek(parsed.DoorKey, parsed.ExpiresAtUtc);
    }

    public async Task<PasscodeTake> TakeAsync(string nonce, TimeSpan tombstoneTtl, CancellationToken cancellationToken)
    {
        var seconds = Math.Max(1, (int)tombstoneTtl.TotalSeconds);
        var db = _redis.GetDatabase();
        var raw = await db.ScriptEvaluateAsync(
            TakeScript,
            new RedisKey[] { LiveKey(nonce), UsedKey(nonce) },
            new RedisValue[] { seconds }).WaitAsync(cancellationToken);

        if (raw.IsNull)
            return new PasscodeTake(PasscodeTakeKind.Missing, null, null);

        var text = raw.ToString();
        if (text.StartsWith("USED|", StringComparison.Ordinal))
        {
            var parsed = ParsePayload(text["USED|".Length..]);
            return new PasscodeTake(PasscodeTakeKind.AlreadyUsed, parsed.DoorKey, parsed.ExpiresAtUtc);
        }

        var live = ParsePayload(text);
        return new PasscodeTake(PasscodeTakeKind.Taken, live.DoorKey, live.ExpiresAtUtc);
    }

    private static (string? DoorKey, DateTime? ExpiresAtUtc) ParsePayload(string payload)
    {
        var split = payload.Split('|');
        if (split.Length < 2 || string.IsNullOrWhiteSpace(split[0]))
            return (null, null);

        if (!long.TryParse(split[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixMs))
            return (split[0], null);

        return (split[0], DateTimeOffset.FromUnixTimeMilliseconds(unixMs).UtcDateTime);
    }

    private static string WelcomeKey(string doorKey) => $"pdks:welcome:{doorKey}";

    private static string BoardKey(string doorKey) => $"pdks:board:v5:{doorKey}";

    private const string BoardStampKey = "pdks:board:stamp";

    private static string LiveKey(string nonce) => $"pdks:nonce:{nonce}";

    private static string UsedKey(string nonce) => $"pdks:used:{nonce}";
}
