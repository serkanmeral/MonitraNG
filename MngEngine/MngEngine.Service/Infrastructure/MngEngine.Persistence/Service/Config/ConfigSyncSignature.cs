using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using MngEngine.Application.Features.EngineConfig;

namespace MngEngine.Persistence.Service.Config;

/// <summary>
/// Asset ve period değişikliklerini tespit etmek için config imzası hesaplar.
/// Değişiklik yoksa sync ve job reschedule atlanabilir.
/// </summary>
public static class ConfigSyncSignature
{
    private const string CacheKey = "engineConfigSignature";

    /// <summary>Asset, period ve SendSchedule/ConfigSyncPeriod bilgilerinden deterministik imza üretir.</summary>
    public static string Compute(EngineConfigSyncResult? result)
    {
        var sb = new StringBuilder();
        sb.Append(result?.SendSchedule ?? "").Append('|').Append(result?.ConfigSyncPeriodMinutes ?? 0).Append(';');
        if (result?.AssetConfigs != null)
        {
            foreach (var ac in result.AssetConfigs.OrderBy(a => $"{a.AgentId}:{a.AssetId}:{a.ItemId ?? ""}"))
            {
                sb.Append(ac.AgentId).Append('|')
                  .Append(ac.AssetId).Append('|')
                  .Append(ac.ItemId ?? "").Append('|')
                  .Append(ac.PeriodExpression ?? "").Append(';');
            }
        }
        var input = sb.ToString();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..16];
    }

    /// <summary>Önceki imzayı cache'ten alır.</summary>
    public static string? GetStored(IMemoryCache cache) =>
        cache.Get<string>(CacheKey);

    /// <summary>Yeni imzayı cache'e yazar.</summary>
    public static void Store(IMemoryCache cache, string signature) =>
        cache.Set(CacheKey, signature);
}
