using System.Collections.Generic;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Lextm.SharpSnmpLib.Security;
using MngSim.Models;

namespace MngSim.Services;

/// <summary>
/// SNMP GET/GETNEXT isteğini parse eder, template'e göre OID değerlerini üretir, ResponseMessage döndürür.
/// </summary>
public class SnmpRequestHandler
{
    private static readonly UserRegistry DefaultRegistry = new();
    private readonly SnmpTemplateRegistry _registry;

    public SnmpRequestHandler(SnmpTemplateRegistry registry) => _registry = registry;

    public byte[]? ProcessRequest(byte[] buffer, int length, VirtualDevice device)
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

        var template = _registry.GetTemplate(device.SnmpTemplate);
        if (template == null)
            return null;

        var responseVars = new List<Variable>();

        foreach (var v in variables)
        {
            var requestedOid = v.Id.ToString();
            string? responseOid;
            ISnmpData? data;

            if (request.Pdu().TypeCode == SnmpType.GetNextRequestPdu)
            {
                var nextOid = template.GetNextOid(requestedOid);
                if (nextOid == null)
                {
                    responseVars.Add(new Variable(new ObjectIdentifier(requestedOid), new EndOfMibView()));
                    continue;
                }
                responseOid = nextOid;
                data = template.GetValueForOid(nextOid, device);
            }
            else
            {
                if (!template.TryGetExactOid(requestedOid, out var exact))
                {
                    responseVars.Add(new Variable(v.Id, new NoSuchInstance()));
                    continue;
                }
                responseOid = exact!;
                data = template.GetValueForOid(responseOid, device);
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
}
