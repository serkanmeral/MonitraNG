using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MngEngine.Application.Interfaces;
using MngEngine.Persistence.Options;
using Serilog;

namespace MngEngine.Persistence.Service.SecEvents;

/// <summary>SIEM Faz 1 S3.1 — syslog UDP listener (çoklu port).</summary>
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

        var listeners = _options.GetEffectiveListeners();
        var tasks = listeners
            .Select(listener => ListenPortAsync(listener, stoppingToken))
            .ToArray();

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // graceful shutdown
        }
        finally
        {
            _logger.Information("SecEvent syslog UDP listener durdu");
        }
    }

    private async Task ListenPortAsync(SecEventSyslogListenerOptions listener, CancellationToken stoppingToken)
    {
        UdpClient? udp = null;
        try
        {
            udp = new UdpClient(listener.UdpPort);
            _logger.Information(
                "SecEvent syslog UDP listener başladı port={Port} type={Type} product={Product}",
                listener.UdpPort,
                listener.SourceType,
                listener.SourceProduct);

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
                var item = _itemBuilder.FromSyslog(raw, packet.RemoteEndPoint, DateTime.UtcNow, listener);
                if (SecEventNxlogIngestGuard.ShouldReject(item, _options.AcceptNxlogIngest))
                {
                    _logger.Debug(
                        "SecEvent NXLog UDP dropped port={Port} host={Host} product={Product}",
                        listener.UdpPort,
                        item.Source?.Host,
                        item.Source?.Product);
                    continue;
                }

                if (SecEventLinuxSyslogIngestGuard.ShouldReject(item, _options.AcceptLinuxSyslogIngest))
                {
                    _logger.Debug(
                        "SecEvent Linux syslog UDP dropped port={Port} host={Host} product={Product}",
                        listener.UdpPort,
                        item.Source?.Host,
                        item.Source?.Product);
                    continue;
                }

                _queue.Enqueue(item);
                _sendCoordinator.RequestFlushIfThresholdReached();
            }
        }
        catch (SocketException ex)
        {
            _logger.Error(ex, "SecEvent syslog UDP bind/dinleme hatası port={Port}", listener.UdpPort);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "SecEvent syslog listener beklenmeyen hata port={Port}", listener.UdpPort);
        }
        finally
        {
            udp?.Dispose();
        }
    }
}
