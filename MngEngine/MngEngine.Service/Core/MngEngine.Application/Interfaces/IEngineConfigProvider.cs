using MngEngine.Application.Features.EngineConfig;

namespace MngEngine.Application.Interfaces;

/// <summary>
/// Decrypt edilmiş Engine config'e erişim sağlar.
/// Config string uygulandıktan sonra cache'ten okunur.
/// </summary>
public interface IEngineConfigProvider
{
    EngineConfigPayload? GetConfig();
}
