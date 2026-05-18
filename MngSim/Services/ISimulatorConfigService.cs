using MngSim.Models;

namespace MngSim.Services;

/// <summary>
/// Simulator konfigürasyonunu okur/yazar (bellek veya dosya).
/// </summary>
public interface ISimulatorConfigService
{
    SimulatorConfig? GetConfig();
    void SetConfig(SimulatorConfig config);
    bool HasValidConfig();
}
