using System.Collections.Generic;
using System.Linq;
using Lextm.SharpSnmpLib;
using MngSim.Models;

namespace MngSim.Services;

/// <summary>
/// Router/Network cihazı SNMP şablonu — MIB-II (1.3.6.1.2.1) sysDescr, sysUpTime, ifTable.
/// </summary>
public class RouterSnmpTemplate : ISnmpTemplate
{
    public string Name => "Router";

    // MIB-II System: 1.3.6.1.2.1.1
    private const string SysDescr = "1.3.6.1.2.1.1.1.0";
    private const string SysUpTime = "1.3.6.1.2.1.1.3.0";
    private const string SysContact = "1.3.6.1.2.1.1.4.0";
    private const string SysName = "1.3.6.1.2.1.1.5.0";
    private const string SysLocation = "1.3.6.1.2.1.1.6.0";
    // Interfaces: 1.3.6.1.2.1.2
    private const string IfNumber = "1.3.6.1.2.1.2.1.0";
    // ifTable .2.2.1: ifIndex.1, ifDescr.1, ifType.1, ifMtu.1, ifSpeed.1, ifAdminStatus.1, ifOperStatus.1, ifInOctets.1, ifOutOctets.1, ...
    private const string IfTableBase = "1.3.6.1.2.1.2.2.1";

    private static readonly string[] SystemOids = { SysDescr, SysUpTime, SysContact, SysName, SysLocation };
    private const int InterfaceCount = 4;
    private static readonly int[] IfColumns = { 1, 2, 3, 4, 5, 7, 8, 10, 16 }; // ifIndex, ifDescr, ifType, ifMtu, ifSpeed, ifAdminStatus, ifOperStatus, ifInOctets, ifOutOctets

    private static readonly string[] OrderedOids = BuildOrderedOids();

    private static string[] BuildOrderedOids()
    {
        var list = new List<string>(SystemOids) { IfNumber };
        for (int i = 1; i <= InterfaceCount; i++)
        {
            foreach (var col in IfColumns)
                list.Add($"{IfTableBase}.{col}.{i}");
        }
        return list.ToArray();
    }

    private static readonly Random Rnd = new();
    private static readonly object RndLock = new();
    private static uint _sysUpTimeTicks;
    private static readonly ulong[] IfInOctets = new ulong[InterfaceCount + 1];
    private static readonly ulong[] IfOutOctets = new ulong[InterfaceCount + 1];

    public string[] GetOrderedOids() => OrderedOids;

    public string? GetNextOid(string requestedOid)
    {
        foreach (var oid in OrderedOids)
        {
            if (string.CompareOrdinal(oid, requestedOid) > 0)
                return oid;
        }
        return null;
    }

    public bool TryGetExactOid(string oid, out string? matchedOid)
    {
        if (OrderedOids.Contains(oid))
        {
            matchedOid = oid;
            return true;
        }
        matchedOid = null;
        return false;
    }

    public ISnmpData? GetValueForOid(string oid, VirtualDevice device)
    {
        if (oid == SysDescr) return new OctetString($"MngSim Router - {device.Name}");
        if (oid == SysUpTime)
        {
            lock (RndLock) { _sysUpTimeTicks += (uint)NextInt(10, 50); }
            return new TimeTicks(_sysUpTimeTicks);
        }
        if (oid == SysContact) return new OctetString($"contact@{device.Id}");
        if (oid == SysName) return new OctetString(device.Name);
        if (oid == SysLocation) return new OctetString(device.Location ?? "Simulated");
        if (oid == IfNumber) return new Integer32(InterfaceCount);

        if (oid.StartsWith(IfTableBase + ".", StringComparison.Ordinal))
        {
            var parts = oid.Split('.');
            if (parts.Length >= 2 && int.TryParse(parts[^1], out var ifIdx) && ifIdx >= 1 && ifIdx <= InterfaceCount
                && int.TryParse(parts[^2], out var col))
            {
                return GetIfTableValue(col, ifIdx, device);
            }
        }
        return null;
    }

    private static ISnmpData GetIfTableValue(int column, int ifIndex, VirtualDevice device)
    {
        lock (RndLock)
        {
            switch (column)
            {
                case 1: return new Integer32(ifIndex);
                case 2: return new OctetString($"eth{ifIndex - 1}");
                case 3: return new Integer32(6); // ethernetCsmacd
                case 4: return new Integer32(1500);
                case 5: return new Gauge32(1000000000); // 1 Gbps
                case 7: return new Integer32(1); // up
                case 8: return new Integer32(1); // up
                case 10:
                    IfInOctets[ifIndex] += (ulong)NextInt(100, 5000);
                    return new Counter32((uint)(IfInOctets[ifIndex] & 0xFFFFFFFF));
                case 16:
                    IfOutOctets[ifIndex] += (ulong)NextInt(50, 3000);
                    return new Counter32((uint)(IfOutOctets[ifIndex] & 0xFFFFFFFF));
            }
        }
        return new OctetString("");
    }

    private static int NextInt(int min, int max) => min + Rnd.Next(max - min + 1);
}
