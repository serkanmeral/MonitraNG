namespace MngSim.Services;

/// <summary>
/// Sanal cihaz dinleyicilerini (HTTP, ileride SNMP/MQTT) başlatır ve durdurur.
/// Başlamadan önce port çakışması kontrol edilir; çakışma varsa başlamaz.
/// </summary>
public interface ISimulatorHostService
{
    bool IsRunning { get; }
    string? LastError { get; }

    /// <summary>Port kontrolü yapar; uygunsa HTTP (ve ileride SNMP/MQTT) dinleyicilerini açar.</summary>
    Task<StartResult> StartAsync(CancellationToken ct = default);

    /// <summary>Tüm dinleyicileri kapatır.</summary>
    Task StopAsync(CancellationToken ct = default);
}

public record StartResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public List<(int Port, string Protocol)>? BusyPorts { get; init; }
}
