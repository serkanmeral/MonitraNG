using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using MngSim.Models;
using Serilog;

namespace MngSim.Services;

public class SimulatorHostService : ISimulatorHostService, IDisposable
{
    private readonly ISimulatorConfigService _configService;
    private readonly IHostMetricGenerator _metricGenerator;
    private readonly SnmpRequestHandler _snmpRequestHandler;
    private readonly Serilog.ILogger _logger = Log.ForContext<SimulatorHostService>();

    private readonly ConcurrentDictionary<int, CancellationTokenSource> _httpListeners = new();
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _snmpListeners = new();
    private volatile bool _running;
    private string? _lastError;

    public bool IsRunning => _running;
    public string? LastError => _lastError;

    public SimulatorHostService(
        ISimulatorConfigService configService,
        IHostMetricGenerator metricGenerator,
        SnmpRequestHandler snmpRequestHandler)
    {
        _configService = configService;
        _metricGenerator = metricGenerator;
        _snmpRequestHandler = snmpRequestHandler;
    }

    public async Task<StartResult> StartAsync(CancellationToken ct = default)
    {
        _running = false;
        foreach (var kv in _httpListeners.ToList())
        {
            try { kv.Value.Cancel(); } catch { /* ignore */ }
        }
        foreach (var kv in _snmpListeners.ToList())
        {
            try { kv.Value.Cancel(); } catch { /* ignore */ }
        }
        _httpListeners.Clear();
        _snmpListeners.Clear();
        await Task.Delay(600, ct).ConfigureAwait(false);

        var config = _configService.GetConfig();
        if (config == null || config.Devices.Count == 0)
        {
            _lastError = "Konfigürasyon veya cihaz yok.";
            return new StartResult { Success = false, ErrorMessage = _lastError };
        }

        var httpDevices = config.Devices
            .Select((d, i) => (Device: d, Index: i))
            .Where(x => (x.Device.IsEnabled ?? true) && string.Equals(x.Device.Protocol, "Http", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var snmpDevices = config.Devices
            .Select((d, i) => (Device: d, Index: i))
            .Where(x => (x.Device.IsEnabled ?? true) && string.Equals(x.Device.Protocol, "Snmp", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (httpDevices.Count == 0 && snmpDevices.Count == 0)
        {
            _lastError = "HTTP veya SNMP cihaz tanımı yok.";
            return new StartResult { Success = false, ErrorMessage = _lastError };
        }

        _lastError = null;
        _running = true;

        foreach (var (device, index) in httpDevices)
        {
            var port = config.HttpBasePort + 1 + index;
            var cts = new CancellationTokenSource();
            _httpListeners[port] = cts;
            var deviceCopy = device;
            _ = Task.Run(() => RunHttpListenerAsync(port, deviceCopy, cts.Token, IPAddress.Loopback), ct);
            _ = Task.Run(() => RunHttpListenerAsync(port, deviceCopy, cts.Token, IPAddress.IPv6Loopback), ct);
        }

        foreach (var (device, index) in snmpDevices)
        {
            var port = config.SnmpBasePort + index;
            var cts = new CancellationTokenSource();
            _snmpListeners[port] = cts;
            var deviceCopy = device;
            _ = Task.Run(() => RunSnmpListenerAsync(port, deviceCopy, cts.Token), ct);
        }

        await Task.Delay(800, ct).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(_lastError))
        {
            var attemptedPorts = new List<(int, string)>();
            foreach (var d in httpDevices)
                attemptedPorts.Add((config.HttpBasePort + 1 + d.Index, "HTTP"));
            foreach (var d in snmpDevices)
                attemptedPorts.Add((config.SnmpBasePort + d.Index, "SNMP"));
            await StopAsync(ct).ConfigureAwait(false);
            return new StartResult
            {
                Success = false,
                ErrorMessage = _lastError,
                BusyPorts = attemptedPorts
            };
        }

        if (httpDevices.Count > 0)
        {
            var httpPorts = httpDevices.Select(d => config.HttpBasePort + 1 + d.Index).ToList();
            _logger.Information("MngSim HTTP dinleyicileri başlatıldı: {Ports}", string.Join(", ", httpPorts));
        }
        if (snmpDevices.Count > 0)
        {
            var snmpPorts = snmpDevices.Select(d => config.SnmpBasePort + d.Index).ToList();
            _logger.Information("MngSim SNMP dinleyicileri başlatıldı: {Ports}", string.Join(", ", snmpPorts));
        }
        return new StartResult { Success = true };
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        _running = false;
        foreach (var kv in _httpListeners)
            kv.Value.Cancel();
        foreach (var kv in _snmpListeners)
            kv.Value.Cancel();
        _httpListeners.Clear();
        _snmpListeners.Clear();
        _logger.Information("MngSim dinleyicileri durduruldu.");
        await Task.CompletedTask;
    }

    private async Task RunHttpListenerAsync(int port, VirtualDevice device, CancellationToken ct, IPAddress listenOn)
    {
        TcpListener? listener = null;
        try
        {
            listener = new TcpListener(listenOn, port);
            listener.Start();
            _logger.Information("HTTP dinleyici dinliyor: {Address}:{Port} (cihaz: {DeviceId})", listenOn, port, device.Id);

            while (_running && !ct.IsCancellationRequested)
            {
                try
                {
                    var client = await listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
                    _logger.Debug("Metrik isteği alındı: port {Port}", port);
                    _ = Task.Run(() => HandleHttpRequestAsync(client, device, ct), ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "HTTP accept hatası port {Port}", port);
                }
            }
        }
        catch (Exception ex)
        {
            if (listenOn.Equals(IPAddress.Loopback))
            {
                _logger.Error(ex, "HTTP dinleyici başlatılamadı port {Port}", port);
                _lastError = $"Port {port}: {ex.Message}";
            }
            else
            {
                _logger.Warning(ex, "HTTP IPv6 dinleyici başlatılamadı port {Port} (IPv4 yeterli olabilir)", port);
            }
        }
        finally
        {
            listener?.Stop();
        }

        await Task.CompletedTask;
    }

    private async Task HandleHttpRequestAsync(TcpClient client, VirtualDevice device, CancellationToken ct)
    {
        try
        {
            await using var stream = client.GetStream();
            var buffer = new byte[4096];
            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), ct);
            var request = Encoding.UTF8.GetString(buffer, 0, read);
            if (!request.StartsWith("GET ", StringComparison.OrdinalIgnoreCase))
            {
                await WriteResponseAsync(stream, 405, "Method Not Allowed").ConfigureAwait(false);
                return;
            }

            var collectedAt = DateTime.UtcNow;
            var metrics = _metricGenerator.GenerateForDevice(device, collectedAt);
            var response = new DeviceMetricsResponse
            {
                CollectedAt = collectedAt,
                DeviceId = device.Id,
                Metrics = metrics
            };
            var json = JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            await WriteResponseAsync(stream, 200, "OK", "application/json", json).ConfigureAwait(false);
            _logger.Information("Metrik yanıtı gönderildi: {DeviceId}, {Count} metrik", device.Id, metrics.Count);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "HTTP istek işlenirken hata");
        }
        finally
        {
            client.Dispose();
        }
    }

    private static async Task WriteResponseAsync(NetworkStream stream, int statusCode, string statusText, string? contentType = null, string? body = null)
    {
        var sb = new StringBuilder();
        sb.Append($"HTTP/1.1 {statusCode} {statusText}\r\n");
        if (body != null)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            sb.Append($"Content-Length: {bytes.Length}\r\n");
            if (!string.IsNullOrEmpty(contentType))
                sb.Append($"Content-Type: {contentType}\r\n");
        }
        sb.Append("\r\n");
        var headerBytes = Encoding.UTF8.GetBytes(sb.ToString());
        await stream.WriteAsync(headerBytes).ConfigureAwait(false);
        if (body != null)
            await stream.WriteAsync(Encoding.UTF8.GetBytes(body)).ConfigureAwait(false);
    }

    private async Task RunSnmpListenerAsync(int port, VirtualDevice device, CancellationToken ct)
    {
        UdpClient udp;
        try
        {
            udp = new UdpClient(port);
        }
        catch (SocketException ex)
        {
            _logger.Error(ex, "SNMP dinleyici başlatılamadı port {Port}", port);
            _lastError = $"Port {port} (SNMP): {ex.Message}";
            return;
        }

        using (udp)
        {
            _logger.Information("SNMP dinleyici dinliyor: 0.0.0.0:{Port} (cihaz: {DeviceId})", port, device.Id);
            while (_running && !ct.IsCancellationRequested)
            {
                try
                {
                    var result = await udp.ReceiveAsync(ct).ConfigureAwait(false);
                var received = result.Buffer;
                if (received.Length == 0) continue;

                var responseBytes = _snmpRequestHandler.ProcessRequest(received, received.Length, device);
                    if (responseBytes != null && responseBytes.Length > 0)
                    {
                        await udp.SendAsync(responseBytes.AsMemory(), result.RemoteEndPoint, ct).ConfigureAwait(false);
                        _logger.Debug("SNMP yanıtı gönderildi: {DeviceId} -> {Remote}", device.Id, result.RemoteEndPoint);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "SNMP istek işlenirken hata port {Port}", port);
                }
            }
        }
    }

    public void Dispose() => _ = StopAsync();
}
