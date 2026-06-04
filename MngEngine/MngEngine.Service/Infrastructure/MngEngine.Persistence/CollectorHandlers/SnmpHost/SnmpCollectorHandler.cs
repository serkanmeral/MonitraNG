using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using MediatR;
using MngEngine.Application.Collector.SnmpHost;
using MngEngine.Application.Features.Ingest;
using MngEngine.Domain.Entities.Asset;

namespace MngEngine.Persistence.CollectorHandlers.SnmpHost;

/// <summary>
/// SNMP v2c toplayıcı. PDU/MngSim şablonu ile uyumlu OID'leri destekler (1.3.6.1.4.1.99999.1.1).
/// </summary>
public class SnmpCollectorHandler : IRequestHandler<SnmpCollectorRequest, SnmpCollectorResponse>
{
    private const string PduBaseOid = "1.3.6.1.4.1.99999.1.1";
    private const int DefaultSnmpPort = 161;
    private const string DefaultCommunity = "public";
    private const int TimeoutMs = 5000;
    private const int MaxRetries = 3;
    private const int RetryDelayMs = 300;

    /// <summary>Collectible code -> OID eşlemesi (PDU/MngSim şablonu 1.3.6.1.4.1.99999.1.1).</summary>
    private static readonly Dictionary<string, string> CodeToOid = new(StringComparer.OrdinalIgnoreCase)
    {
        ["deviceName"] = $"{PduBaseOid}.1",
        ["voltage"] = $"{PduBaseOid}.2",
        ["inputVoltage"] = $"{PduBaseOid}.2",
        ["current"] = $"{PduBaseOid}.3",
        ["inputCurrent"] = $"{PduBaseOid}.3",
        ["inputCurrentX10"] = $"{PduBaseOid}.3",
        ["power"] = $"{PduBaseOid}.4",
        ["activePower"] = $"{PduBaseOid}.4",
        ["activePowerW"] = $"{PduBaseOid}.4",
        ["temperature"] = $"{PduBaseOid}.5",
        ["temp"] = $"{PduBaseOid}.5",
        ["outletCount"] = $"{PduBaseOid}.6",
        ["heartbeat"] = $"{PduBaseOid}.1",
    };

    static SnmpCollectorHandler()
    {
        for (var i = 1; i <= 8; i++)
            CodeToOid[$"outletStatus.{i}"] = $"{PduBaseOid}.7.{i}";
        CodeToOid["outletStatus"] = $"{PduBaseOid}.7.1";
    }

    public async Task<SnmpCollectorResponse> Handle(SnmpCollectorRequest request, CancellationToken cancellationToken)
    {
        var conn = request.Asset?.ConnectionInfo
            ?? throw new InvalidOperationException($"Asset {request.Asset?.Asset_Id ?? "?"}: ConnectionInfo eksik.");
        var assetId = request.Asset.Asset_Id;
        var host = conn.Address ?? "";
        var community = conn.Password ?? DefaultCommunity;
        var port = conn.Port > 0 ? conn.Port : DefaultSnmpPort;

        if (string.IsNullOrWhiteSpace(host))
            throw new InvalidOperationException($"Asset {assetId}: SNMP için ConnectionInfo.address gereklidir.");

        var ip = ResolveHost(host);
        var endpoint = new IPEndPoint(ip, port);
        var communityOctet = new OctetString(community);

        var collectibles = request.Asset.CollectibleItems ?? [];
        var variables = new List<Variable>();
        var codes = new List<string>();

        foreach (var c in collectibles)
        {
            var code = (c?.Code ?? "").Trim();
            if (string.IsNullOrEmpty(code)) continue;
            var oid = CodeToOid.TryGetValue(code, out var o) ? o : $"{PduBaseOid}.1";
            variables.Add(new Variable(new ObjectIdentifier(oid)));
            codes.Add(code);
        }

        if (variables.Count == 0)
        {
            variables.Add(new Variable(new ObjectIdentifier($"{PduBaseOid}.1")));
            codes.Add("heartbeat");
        }

        var metrics = new List<IngestMetric>();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeoutMs * MaxRetries + RetryDelayMs * MaxRetries);

        Exception? lastEx = null;
        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var result = await Task.Run(() =>
                    Messenger.Get(VersionCode.V2, endpoint, communityOctet, variables, TimeoutMs), cts.Token);

                for (var i = 0; i < result.Count && i < codes.Count; i++)
                {
                    var code = codes[i];
                    var val = SnmpValueToObject(result[i].Data);
                    if (val != null)
                        metrics.Add(new IngestMetric { CollectibleCode = code, Value = val, Unit = null });
                }
                lastEx = null;
                break;
            }
            catch (OperationCanceledException)
            {
                lastEx = new InvalidOperationException($"Asset {assetId}: SNMP isteği zaman aşımına uğradı ({TimeoutMs}ms)");
                break;
            }
            catch (SocketException ex)
            {
                lastEx = ex;
                if (attempt < MaxRetries)
                    await Task.Delay(RetryDelayMs, cts.Token);
            }
            catch (Lextm.SharpSnmpLib.Messaging.TimeoutException ex)
            {
                lastEx = ex;
                if (attempt < MaxRetries)
                    await Task.Delay(RetryDelayMs, cts.Token);
            }
            catch (Exception ex) when (ex.Message?.Contains("timed out", StringComparison.OrdinalIgnoreCase) == true)
            {
                lastEx = ex;
                if (attempt < MaxRetries)
                    await Task.Delay(RetryDelayMs, cts.Token);
            }
        }

        if (lastEx != null)
        {
            var hint = (host.Contains("localhost", StringComparison.OrdinalIgnoreCase))
                ? " Not: MngEngine Docker içinde çalışıyorsa 'localhost' erişilemez; config'te host.docker.internal veya host IP kullanın."
                : "";
            var msg = lastEx is Lextm.SharpSnmpLib.Messaging.TimeoutException or System.TimeoutException
                ? $"Asset {assetId}: SNMP zaman aşımı ({TimeoutMs}ms, {MaxRetries} deneme). {host}:{port} erişilebilir mi?{hint}"
                : $"Asset {assetId}: SNMP bağlantı hatası ({MaxRetries} deneme başarısız): {lastEx.Message}. MngSim {host}:{port} dinliyor mu?{hint}";
            throw new InvalidOperationException(msg, lastEx);
        }

        if (metrics.Count == 0)
            metrics.Add(new IngestMetric { CollectibleCode = "heartbeat", Value = 1, Unit = null });

        return new SnmpCollectorResponse { Metrics = metrics, Result = $"SNMP {assetId}" };
    }

    private static IPAddress ResolveHost(string host)
    {
        var h = host.Trim();
        if (string.Equals(h, "localhost", StringComparison.OrdinalIgnoreCase))
            return IPAddress.Loopback;
        if (IPAddress.TryParse(h, out var ip)) return ip;
        var addrs = Dns.GetHostEntry(h).AddressList;
        var ipv4 = addrs.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            ?? addrs.FirstOrDefault();
        return ipv4 ?? throw new InvalidOperationException($"Host çözümlenemedi: {host}");
    }

    private static object? SnmpValueToObject(ISnmpData? data)
    {
        if (data == null) return null;
        if (data is Integer32 i32) return i32.ToInt32();
        if (data is Gauge32 g32) return (long)g32.ToUInt32();
        if (data is Counter32 c32) return (long)c32.ToUInt32();
        if (data is OctetString oct) return oct.ToString();
        return data.ToString();
    }
}
