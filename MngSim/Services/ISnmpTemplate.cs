using Lextm.SharpSnmpLib;
using MngSim.Models;

namespace MngSim.Services;

/// <summary>
/// SNMP OID şablonu — desteklenen OID’ler, GET/GETNEXT mantığı ve değer üretimi.
/// </summary>
public interface ISnmpTemplate
{
    string Name { get; }
    string[] GetOrderedOids();
    string? GetNextOid(string requestedOid);
    bool TryGetExactOid(string oid, out string? matchedOid);
    ISnmpData? GetValueForOid(string oid, VirtualDevice device);
}
