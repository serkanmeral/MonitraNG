namespace MngSim.Services;

/// <summary>
/// Portların kullanılabilir olup olmadığını kontrol eder; çakışma varsa hata bilgisi döner.
/// </summary>
public interface IPortAvailabilityChecker
{
    /// <summary>
    /// Verilen portların hepsinin boş olduğunu doğrular. Bir port meşgulse (port, hata) listesine eklenir.
    /// </summary>
    PortCheckResult CheckPorts(IEnumerable<int> ports);
}

public record PortCheckResult
{
    public bool AllFree => BusyPorts.Count == 0;
    public List<(int Port, string Error)> BusyPorts { get; init; } = new();
}
