using MngReactor.Application.Features.Engine;

namespace MngReactor.Application.Abstractions.Engine;

/// <summary>
/// Engine'in periyodik status (heartbeat + hatalar) bildirimini işler.
/// mon_engines.lastSeenAt ve mon_engines.lastErrors günceller.
/// </summary>
public interface IEngineStatusProcessing
{
    /// <summary>
    /// Engine status (heartbeat, errors) alır, mon_engines'i günceller.
    /// </summary>
    /// <param name="request">Status payload (engineId, domain, health, errors)</param>
    /// <param name="domainFromToken">Token'dan gelen domain</param>
    /// <param name="accessToken">Bearer token</param>
    /// <param name="cancellationToken">İptal token</param>
    /// <returns>Başarılı ise true; engine bulunamazsa veya domain uyuşmazsa false</returns>
    Task<bool> ProcessStatusAsync(
        EngineStatusRequest request,
        string domainFromToken,
        string? accessToken,
        CancellationToken cancellationToken = default);
}
