using System.Collections.Generic;
using System.Linq;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Lextm.SharpSnmpLib.Security;
using MngSim.Models;

namespace MngSim.Services;

/// <summary>
/// Gelen SNMP GET/GETNEXT isteğini parse eder, PDU OID tablosundan değerleri doldurur, ResponseMessage üretir.
/// </summary>
public class SnmpPduRequestHandler
{
    private static readonly UserRegistry DefaultRegistry = new();

    /// <summary>
    /// Gelen UDP buffer'ı parse edip yanıt bytes döndürür. Desteklenmiyorsa veya hata varsa null.
    /// </summary>
    public static byte[]? ProcessRequest(byte[] buffer, int length, PduSnmpValues values)
    {
        if (length <= 0 || buffer == null || buffer.Length < length)
            return null;

        IList<ISnmpMessage>? messages;
        try
        {
            messages = MessageFactory.ParseMessages(buffer, 0, length, DefaultRegistry);
        }
        catch
        {
            return null;
        }

        if (messages == null || messages.Count == 0)
            return null;

        var request = messages[0];
        var version = request.Version;
        if (version != VersionCode.V1 && version != VersionCode.V2)
            return null;

        var community = request.Community();
        var requestId = request.RequestId();
        var variables = request.Variables();
        if (variables == null || variables.Count == 0)
            return null;

        bool isGetNext = request.Pdu().TypeCode == SnmpType.GetNextRequestPdu || request.Pdu().TypeCode == SnmpType.GetRequestPdu;
        var responseVars = new List<Variable>();

        foreach (var v in variables)
        {
            var requestedOid = v.Id.ToString();
            string? responseOid;
            ISnmpData? data;

            if (request.Pdu().TypeCode == SnmpType.GetNextRequestPdu)
            {
                var nextOid = SnmpPduOids.GetNextOid(requestedOid);
                if (nextOid == null)
                {
                    responseVars.Add(new Variable(new ObjectIdentifier(requestedOid), new EndOfMibView()));
                    continue;
                }
                responseOid = nextOid;
                data = GetValueForOid(nextOid, values);
            }
            else
            {
                if (!SnmpPduOids.TryGetExactOid(requestedOid, out var exact))
                {
                    responseVars.Add(new Variable(v.Id, new NoSuchInstance()));
                    continue;
                }
                responseOid = exact!;
                data = GetValueForOid(responseOid, values);
            }

            if (data != null)
                responseVars.Add(new Variable(new ObjectIdentifier(responseOid), data));
            else
                responseVars.Add(new Variable(new ObjectIdentifier(responseOid), new NoSuchInstance()));
        }

        var response = new ResponseMessage(
            requestId,
            version,
            community,
            ErrorCode.NoError,
            0,
            responseVars);

        return response.ToBytes();
    }

    private static ISnmpData? GetValueForOid(string oid, PduSnmpValues values)
    {
        if (oid.EndsWith(".1"))
            return new OctetString(values.DeviceName);
        if (oid.EndsWith(".2"))
            return new Gauge32(values.InputVoltage);
        if (oid.EndsWith(".3"))
            return new Gauge32(values.InputCurrentX10);
        if (oid.EndsWith(".4"))
            return new Gauge32(values.ActivePowerW);
        if (oid.EndsWith(".5"))
            return new Integer32(values.Temperature);
        if (oid.EndsWith(".6"))
            return new Integer32(values.OutletCount);
        if (oid.StartsWith(SnmpPduOids.Base + ".7.", StringComparison.Ordinal))
        {
            var suffix = oid.AsSpan((SnmpPduOids.Base + ".7.").Length);
            if (int.TryParse(suffix.ToString(), out var idx) && idx >= 1 && idx <= values.OutletStatus.Count)
                return new Integer32(values.OutletStatus[idx - 1]);
        }
        return null;
    }
}
