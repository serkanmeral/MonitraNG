namespace MngSim.Models;

/// <summary>
/// SNMP PDU simülatörü için anlık değerler — handler her istekte bu DTO ile yanıt oluşturur.
/// </summary>
public class PduSnmpValues
{
    public string DeviceName { get; set; } = "";
    public uint InputVoltage { get; set; }
    public uint InputCurrentX10 { get; set; }
    public uint ActivePowerW { get; set; }
    public int Temperature { get; set; }
    public int OutletCount { get; set; }
    public IReadOnlyList<int> OutletStatus { get; set; } = Array.Empty<int>();
}
