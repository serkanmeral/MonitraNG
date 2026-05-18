using MngSim.Models;

namespace MngSim.Services;

/// <summary>
/// PDU benzeri SNMP metrikleri üretir (gerilim, akım, güç, sıcaklık, priz durumları).
/// </summary>
public interface IPduMetricGenerator
{
    PduSnmpValues Generate(VirtualDevice device);
}
