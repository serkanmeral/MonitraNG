using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MngLogs.Agent.Configuration;

namespace MngLogs.Agent.LocalUi;

public interface ILocalUiPinAuth
{
    LocalUiAuthStatus GetStatus(string? token);
    LocalUiAuthResult Setup(string pin, string pinConfirm);
    LocalUiAuthResult Unlock(string pin);
    void Lock(string? token);
    LocalUiAuthResult ChangePin(string? token, string currentPin, string newPin, string newPinConfirm);
    bool TryValidateSession(string? token, out string? error);

    /// <summary>CLI recovery: delete PIN file and clear sessions.</summary>
    void ResetPin();

    /// <summary>CLI recovery: set/overwrite PIN without an existing UI session.</summary>
    LocalUiAuthResult AdminSetPin(string pin, string pinConfirm);
}

public sealed class LocalUiAuthStatus
{
    public bool Configured { get; init; }
    public bool Unlocked { get; init; }
    public DateTime? SessionExpiresAtUtc { get; init; }
    public DateTime? LockedUntilUtc { get; init; }
    public int FailedAttempts { get; init; }
    public int SessionTtlSeconds { get; init; } = LocalUiPinAuth.SessionTtlSeconds;
    public int MinPinLength { get; init; } = LocalUiPinAuth.MinPinLength;
}

public sealed class LocalUiAuthResult
{
    public bool Ok { get; init; }
    public string? Error { get; init; }
    public string? Token { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }
    public DateTime? LockedUntilUtc { get; init; }
}

/// <summary>
/// Local UI write protection: PBKDF2 PIN in ui-auth.json + in-memory session tokens.
/// </summary>
public sealed class LocalUiPinAuth : ILocalUiPinAuth
{
    public const int MinPinLength = 4;
    public const int MaxPinLength = 64;
    public const int SessionTtlSeconds = 20 * 60;
    public const int MaxFailedAttempts = 5;
    public const int LockoutSeconds = 60;
    public const string TokenHeaderName = "X-Local-Ui-Token";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IAgentConfigStore _config;
    private readonly object _gate = new();
    private readonly ConcurrentDictionary<string, DateTime> _sessions = new(StringComparer.Ordinal);
    private AuthFileState _state;
    private int _failedAttempts;
    private DateTime? _lockedUntilUtc;

    public LocalUiPinAuth(IAgentConfigStore config)
    {
        _config = config;
        _state = LoadOrEmpty();
    }

    public LocalUiAuthStatus GetStatus(string? token)
    {
        CleanupExpiredSessions();
        var unlocked = IsSessionValid(token, out var expires);
        lock (_gate)
        {
            return new LocalUiAuthStatus
            {
                Configured = !string.IsNullOrEmpty(_state.PinHashBase64),
                Unlocked = unlocked,
                SessionExpiresAtUtc = unlocked ? expires : null,
                LockedUntilUtc = _lockedUntilUtc,
                FailedAttempts = _failedAttempts,
                SessionTtlSeconds = SessionTtlSeconds,
                MinPinLength = MinPinLength
            };
        }
    }

    public LocalUiAuthResult Setup(string pin, string pinConfirm)
    {
        CleanupExpiredSessions();
        lock (_gate)
        {
            if (!string.IsNullOrEmpty(_state.PinHashBase64))
                return Fail("PIN zaten tanımlı. Değiştirmek için oturum açın.");

            var validation = ValidatePinPair(pin, pinConfirm);
            if (validation is not null)
                return Fail(validation);

            PersistPin(pin!);
            var (token, expires) = IssueSessionUnlocked();
            return new LocalUiAuthResult
            {
                Ok = true,
                Token = token,
                ExpiresAtUtc = expires
            };
        }
    }

    public LocalUiAuthResult Unlock(string pin)
    {
        CleanupExpiredSessions();
        lock (_gate)
        {
            if (string.IsNullOrEmpty(_state.PinHashBase64))
                return Fail("Önce PIN oluşturun.");

            if (_lockedUntilUtc is { } until && until > DateTime.UtcNow)
            {
                return new LocalUiAuthResult
                {
                    Ok = false,
                    Error = $"Çok fazla başarısız deneme. {until:HH:mm:ss} UTC sonrasına kadar kilitli.",
                    LockedUntilUtc = until
                };
            }

            if (!VerifyPin(pin, _state))
            {
                _failedAttempts++;
                if (_failedAttempts >= MaxFailedAttempts)
                {
                    _lockedUntilUtc = DateTime.UtcNow.AddSeconds(LockoutSeconds);
                    _failedAttempts = 0;
                    return new LocalUiAuthResult
                    {
                        Ok = false,
                        Error = $"PIN hatalı. {LockoutSeconds} sn kilitlendi.",
                        LockedUntilUtc = _lockedUntilUtc
                    };
                }

                var left = MaxFailedAttempts - _failedAttempts;
                return Fail($"PIN hatalı. Kalan deneme: {left}");
            }

            _failedAttempts = 0;
            _lockedUntilUtc = null;
            var (token, expires) = IssueSessionUnlocked();
            return new LocalUiAuthResult
            {
                Ok = true,
                Token = token,
                ExpiresAtUtc = expires
            };
        }
    }

    public void Lock(string? token)
    {
        if (!string.IsNullOrWhiteSpace(token))
            _sessions.TryRemove(token.Trim(), out _);
    }

    public LocalUiAuthResult ChangePin(string? token, string currentPin, string newPin, string newPinConfirm)
    {
        CleanupExpiredSessions();
        if (!TryValidateSession(token, out var sessionError))
            return Fail(sessionError ?? "Oturum geçersiz.");

        lock (_gate)
        {
            if (!VerifyPin(currentPin, _state))
                return Fail("Mevcut PIN hatalı.");

            var validation = ValidatePinPair(newPin, newPinConfirm);
            if (validation is not null)
                return Fail(validation);

            PersistPin(newPin!);
            return new LocalUiAuthResult { Ok = true };
        }
    }

    public void ResetPin()
    {
        lock (_gate)
        {
            _sessions.Clear();
            _failedAttempts = 0;
            _lockedUntilUtc = null;
            _state = new AuthFileState();
            try
            {
                var path = AuthFilePath();
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Best-effort; empty state still unlocks setup flow after restart.
            }
        }
    }

    public LocalUiAuthResult AdminSetPin(string pin, string pinConfirm)
    {
        CleanupExpiredSessions();
        lock (_gate)
        {
            var validation = ValidatePinPair(pin, pinConfirm);
            if (validation is not null)
                return Fail(validation);

            _sessions.Clear();
            _failedAttempts = 0;
            _lockedUntilUtc = null;
            PersistPin(pin!);
            return new LocalUiAuthResult { Ok = true };
        }
    }

    public bool TryValidateSession(string? token, out string? error)
    {
        CleanupExpiredSessions();
        if (string.IsNullOrEmpty(_state.PinHashBase64))
        {
            // No PIN yet: allow writes only through setup flow; treat as locked for protected APIs.
            error = "Politika koruması için önce PIN oluşturun.";
            return false;
        }

        if (!IsSessionValid(token, out _))
        {
            error = "Politika oturumu yok veya süresi dolmuş. PIN ile kilidi açın.";
            return false;
        }

        error = null;
        return true;
    }

    private (string Token, DateTime Expires) IssueSessionUnlocked()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var expires = DateTime.UtcNow.AddSeconds(SessionTtlSeconds);
        _sessions[token] = expires;
        return (token, expires);
    }

    private bool IsSessionValid(string? token, out DateTime? expires)
    {
        expires = null;
        if (string.IsNullOrWhiteSpace(token))
            return false;
        if (!_sessions.TryGetValue(token.Trim(), out var exp))
            return false;
        if (exp <= DateTime.UtcNow)
        {
            _sessions.TryRemove(token.Trim(), out _);
            return false;
        }

        // Sliding expiration
        var renewed = DateTime.UtcNow.AddSeconds(SessionTtlSeconds);
        _sessions[token.Trim()] = renewed;
        expires = renewed;
        return true;
    }

    private void CleanupExpiredSessions()
    {
        var now = DateTime.UtcNow;
        foreach (var kv in _sessions)
        {
            if (kv.Value <= now)
                _sessions.TryRemove(kv.Key, out _);
        }

        lock (_gate)
        {
            if (_lockedUntilUtc is { } until && until <= now)
                _lockedUntilUtc = null;
        }
    }

    private void PersistPin(string pin)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Pbkdf2(pin, salt);
        _state = new AuthFileState
        {
            Algorithm = "PBKDF2-SHA256",
            Iterations = 100_000,
            SaltBase64 = Convert.ToBase64String(salt),
            PinHashBase64 = Convert.ToBase64String(hash),
            UpdatedAtUtc = DateTime.UtcNow
        };
        Save(_state);
    }

    private static string? ValidatePinPair(string? pin, string? confirm)
    {
        if (string.IsNullOrEmpty(pin) || pin.Length < MinPinLength)
            return $"PIN en az {MinPinLength} karakter olmalı.";
        if (pin.Length > MaxPinLength)
            return $"PIN en fazla {MaxPinLength} karakter olabilir.";
        if (!string.Equals(pin, confirm, StringComparison.Ordinal))
            return "PIN doğrulaması eşleşmiyor.";
        return null;
    }

    private static bool VerifyPin(string? pin, AuthFileState state)
    {
        if (string.IsNullOrEmpty(pin) || string.IsNullOrEmpty(state.PinHashBase64) || string.IsNullOrEmpty(state.SaltBase64))
            return false;
        try
        {
            var salt = Convert.FromBase64String(state.SaltBase64);
            var expected = Convert.FromBase64String(state.PinHashBase64);
            var actual = Pbkdf2(pin, salt, state.Iterations <= 0 ? 100_000 : state.Iterations);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch
        {
            return false;
        }
    }

    private static byte[] Pbkdf2(string pin, byte[] salt, int iterations = 100_000)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(pin),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            32);
    }

    private AuthFileState LoadOrEmpty()
    {
        try
        {
            var path = AuthFilePath();
            if (!File.Exists(path))
                return new AuthFileState();
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AuthFileState>(json, JsonOptions) ?? new AuthFileState();
        }
        catch
        {
            return new AuthFileState();
        }
    }

    private void Save(AuthFileState state)
    {
        var dir = _config.ResolveConfigDirectory();
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "ui-auth.json");
        File.WriteAllText(path, JsonSerializer.Serialize(state, JsonOptions));
    }

    private string AuthFilePath() =>
        Path.Combine(_config.ResolveConfigDirectory(), "ui-auth.json");

    private static LocalUiAuthResult Fail(string error) => new() { Ok = false, Error = error };

    private sealed class AuthFileState
    {
        public string Algorithm { get; set; } = "PBKDF2-SHA256";
        public int Iterations { get; set; } = 100_000;
        public string SaltBase64 { get; set; } = string.Empty;
        public string PinHashBase64 { get; set; } = string.Empty;
        public DateTime? UpdatedAtUtc { get; set; }
    }
}
