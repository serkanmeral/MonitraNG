using System.Collections.Generic;
using System.Linq;
using Lextm.SharpSnmpLib;

namespace MngSim.Services;

/// <summary>
/// PDU simülatörü OID ağacı: 1.3.6.1.4.1.99999.1.1 (deviceName, voltage, current, power, temp, outletCount, outletStatus.1..N).
/// GET/GETNEXT için sıralı OID listesi ve sonraki OID bulma.
/// </summary>
public static class SnmpPduOids
{
    public const string Base = "1.3.6.1.4.1.99999.1.1";
    public const int DefaultOutletCount = 8;

    /// <summary>Desteklenen OID'ler lexicographic sırada (GETNEXT için).</summary>
    private static readonly string[] OrderedOids = BuildOrderedOids();

    private static string[] BuildOrderedOids()
    {
        var list = new List<string>
        {
            $"{Base}.1",  // deviceName
            $"{Base}.2",  // inputVoltage
            $"{Base}.3",  // inputCurrentX10
            $"{Base}.4",  // activePowerW
            $"{Base}.5",  // temperature
            $"{Base}.6",  // outletCount
        };
        for (int i = 1; i <= DefaultOutletCount; i++)
            list.Add($"{Base}.7.{i}"); // outletStatus.1 .. .8
        return list.ToArray();
    }

    /// <summary>İstenen OID'den sonraki OID'yi döndürür; yoksa null.</summary>
    public static string? GetNextOid(string requestedOid)
    {
        foreach (var oid in OrderedOids)
        {
            if (string.CompareOrdinal(oid, requestedOid) > 0)
                return oid;
        }
        return null;
    }

    /// <summary>OID tam eşleşme ile tabloda var mı?</summary>
    public static bool TryGetExactOid(string oid, out string? matchedOid)
    {
        if (OrderedOids.Contains(oid))
        {
            matchedOid = oid;
            return true;
        }
        matchedOid = null;
        return false;
    }

    /// <summary>İlk OID (walk başlangıcı için).</summary>
    public static string FirstOid => OrderedOids[0];

    /// <summary>Sıralı OID listesi (template kullanımı için).</summary>
    public static string[] GetOrderedOidsInternal() => OrderedOids;
}
