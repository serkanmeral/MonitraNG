using System.Security.Cryptography;
using System.Text;
using MngLogs.Agent.Configuration;

namespace MngLogs.Agent.Dlp;

/// <summary>Loopback evaluate key at <c>%ProgramData%\MngLogs\Agent\dlp-local.key</c> (POLICY.md K10).</summary>
public sealed class DlpLocalKeyStore
{
    public const string HeaderName = "X-MngLogs-DlpKey";
    public const string FileName = "dlp-local.key";

    private readonly IAgentConfigStore _config;
    private readonly object _gate = new();
    private string? _key;

    public DlpLocalKeyStore(IAgentConfigStore config)
    {
        _config = config;
    }

    public string GetOrCreate()
    {
        lock (_gate)
        {
            if (!string.IsNullOrEmpty(_key))
                return _key;

            var path = Path.Combine(_config.ResolveDataDirectory(), FileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            if (File.Exists(path))
            {
                var existing = File.ReadAllText(path).Trim();
                if (existing.Length >= 16)
                {
                    _key = existing;
                    return _key;
                }
            }

            var bytes = RandomNumberGenerator.GetBytes(32);
            var generated = Convert.ToHexString(bytes).ToLowerInvariant();
            File.WriteAllText(path, generated, Encoding.ASCII);
            _key = generated;
            return _key;
        }
    }

    public bool IsValid(string? presented)
    {
        var expected = GetOrCreate();
        if (string.IsNullOrEmpty(presented) || presented.Length != expected.Length)
            return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(presented),
            Encoding.ASCII.GetBytes(expected));
    }
}
