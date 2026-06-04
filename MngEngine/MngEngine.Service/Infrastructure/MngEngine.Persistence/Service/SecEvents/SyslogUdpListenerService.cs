using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MngEngine.Application.Interfaces;
using MngEngine.Persistence.Options;
using Serilog;

namespace MngEngine.Persistence.Service.SecEvents;

/// <summary>SIEM Faz 1 S3.1 — syslog UDP listener.</summary>
public sealed class SyslogUdpListenerService : BackgroundService
{
    private readonly ILogger _logger;
    private readonly SecEventQueueOptions _options;
    private readonly ISecEventBatchQueue _queue;
    private readonly SecEventSyslogItemBuilder _itemBuilder;
    private readonly SecEventSendCoordinator _sendCoordinator;

    public SyslogUdpListenerService(
        ILogger logger,
        IOptions<SecEventQueueOptions> options,
        ISecEventBatchQueue queue,
        SecEventSyslogItemBuilder itemBuilder,
        SecEventSendCoordinator sendCoordinator)
    {
        _logger = logger;
        _options = options.Value;
        _queue = queue;
        _itemBuilder = itemBuilder;
        _sendCoordinator = sendCoordinator;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.Information("SecEvent syslog listener devre dışı (MngEngine:SecEventQueue:Enabled=false)");
            return;
        }

        UdpClient? udp = null;
        try
        {
            udp = new UdpClient(_options.UdpPort);
            _logger.Information("SecEvent syslog UDP listener başladı port={Port}", _options.UdpPort);

            while (!stoppingToken.IsCancellationRequested)
            {
                UdpReceiveResult packet;
                try
                {
                    packet = await udp.ReceiveAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                var raw = Encoding.UTF8.GetString(packet.Buffer);
                var item = _itemBuilder.FromSyslog(raw, packet.RemoteEndPoint, DateTime.UtcNow);
                _queue.Enqueue(item);
                _sendCoordinator.RequestFlushIfThresholdReached();
            }
        }
        catch (SocketException ex)
        {
            _logger.Error(ex, "SecEvent syslog UDP bind/dinleme hatası port={Port}", _options.UdpPort);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "SecEvent syslog listener beklenmeyen hata");
        }
        finally
        {
            udp?.Dispose();
            _logger.Information("SecEvent syslog UDP listener durdu");
        }
    }
}
