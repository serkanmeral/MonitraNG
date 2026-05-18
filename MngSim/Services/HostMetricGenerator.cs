using MngSim.Models;

namespace MngSim.Services;

/// <summary>
/// Host tipi sentetik metrikler: cpu_usage, memory_used, memory_total, disk_usage.
/// Rastgele ve hafif salınımlı değerler üretir.
/// </summary>
public class HostMetricGenerator : IHostMetricGenerator
{
    private static readonly Random Rnd = new();
    private static readonly object RndLock = new();

    public List<MetricItem> GenerateForDevice(VirtualDevice device, DateTime collectedAt)
    {
        _ = device;
        _ = collectedAt;
        var metrics = new List<MetricItem>();

        var cpu = NextDouble(20, 80);
        metrics.Add(new MetricItem { CollectibleCode = "cpu_usage", Value = Math.Round(cpu, 2), Unit = "%" });

        var totalKb = 8 * 1024 * 1024;
        metrics.Add(new MetricItem { CollectibleCode = "memory_total", Value = totalKb, Unit = "KB" });

        var usedKb = (long)(totalKb * NextDouble(0.4, 0.8));
        metrics.Add(new MetricItem { CollectibleCode = "memory_used", Value = usedKb, Unit = "KB" });

        var totalGb = 500;
        var usedPercent = NextDouble(30, 85);
        var usedGb = (long)(totalGb * usedPercent / 100);
        var freeGb = totalGb - usedGb;
        var diskUsage = new
        {
            total = totalGb * 1024 * 1024 * 1024L,
            used = usedGb * 1024 * 1024 * 1024L,
            free = freeGb * 1024 * 1024 * 1024L,
            percent = Math.Round(usedPercent, 2)
        };
        metrics.Add(new MetricItem { CollectibleCode = "disk_usage", Value = diskUsage, Unit = null });

        return metrics;
    }

    private static double NextDouble(double min, double max)
    {
        lock (RndLock)
        {
            return min + (max - min) * Rnd.NextDouble();
        }
    }
}
