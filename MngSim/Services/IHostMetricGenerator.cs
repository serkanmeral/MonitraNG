using MngSim.Models;

namespace MngSim.Services;

/// <summary>
/// Host tipi sentetik metrikleri üretir (cpu_usage, memory_used, memory_total, disk_usage).
/// HTTP/SNMP/MQTT cihaz yanıtları için kullanılır.
/// </summary>
public interface IHostMetricGenerator
{
    /// <summary>
    /// Sanal cihaz için metrik listesi üretir (Engine formatına uyumlu).
    /// </summary>
    List<MetricItem> GenerateForDevice(VirtualDevice device, DateTime collectedAt);
}
