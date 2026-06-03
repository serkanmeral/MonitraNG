namespace MngReactor.Application.Abstractions.Engine;

/// <summary>
/// Bir asset'i kullanan agent'ların engineId listesini döner.
/// </summary>
public interface IEngineIdsForAssetResolver
{
    Task<IReadOnlyList<string>> GetEngineIdsForAssetAsync(string domain, string assetId, string? accessToken = null, CancellationToken cancellationToken = default);
}
