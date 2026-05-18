using System.Collections.Generic;
using System.Linq;
using Lextm.SharpSnmpLib;
using MngSim.Models;

namespace MngSim.Services;

/// <summary>
/// PDU (Power Distribution Unit) SNMP şablonu — 1.3.6.1.4.1.99999.1.1
/// </summary>
public class PduSnmpTemplate : ISnmpTemplate
{
    public string Name => "Pdu";
    private const string Base = SnmpPduOids.Base;
    private static readonly string[] OrderedOids = SnmpPduOids.GetOrderedOidsInternal();

    public string[] GetOrderedOids() => OrderedOids;

    public string? GetNextOid(string requestedOid) => SnmpPduOids.GetNextOid(requestedOid);

    public bool TryGetExactOid(string oid, out string? matchedOid) => SnmpPduOids.TryGetExactOid(oid, out matchedOid);

    public ISnmpData? GetValueForOid(string oid, VirtualDevice device)
    {
        var values = _pduGenerator.Generate(device);
        if (oid.EndsWith(".1")) return new OctetString(values.DeviceName);
        if (oid.EndsWith(".2")) return new Gauge32(values.InputVoltage);
        if (oid.EndsWith(".3")) return new Gauge32(values.InputCurrentX10);
        if (oid.EndsWith(".4")) return new Gauge32(values.ActivePowerW);
        if (oid.EndsWith(".5")) return new Integer32(values.Temperature);
        if (oid.EndsWith(".6")) return new Integer32(values.OutletCount);
        if (oid.StartsWith(Base + ".7.", StringComparison.Ordinal))
        {
            var suffix = oid.AsSpan((Base + ".7.").Length);
            if (int.TryParse(suffix.ToString(), out var idx) && idx >= 1 && idx <= values.OutletStatus.Count)
                return new Integer32(values.OutletStatus[idx - 1]);
        }
        return null;
    }

    private readonly IPduMetricGenerator _pduGenerator;

    public PduSnmpTemplate(IPduMetricGenerator pduGenerator) => _pduGenerator = pduGenerator;
}
