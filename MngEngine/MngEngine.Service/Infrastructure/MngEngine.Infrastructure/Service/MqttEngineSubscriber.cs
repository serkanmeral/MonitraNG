using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MngEngine.Application.EngineCommand;
using MngEngine.Application.Interfaces;
using MQTTnet;
using MQTTnet.Client;
using Serilog;

namespace MngEngine.Infrastructure.Service;

public class MqttEngineSubscriber : IMqttEngineSubscriber
{
    private const string SyncTopicTemplate = "monitoring/{0}/engine/{1}/sync";
    private const string CommandTopicTemplate = "monitoring/{0}/engine/{1}/command";

    private readonly ILogger _logger;
    private readonly IEngineConfigProvider _configProvider;
    private readonly MqttEngineOptions _options;
    private IMqttClient? _client;
    private MqttClientOptions? _clientOptions;

    public event EventHandler? SyncRequested;
    public event EventHandler<MqttCommandReceivedEventArgs>? CommandReceived;
    public bool IsConnected => _client?.IsConnected ?? false;

    public MqttEngineSubscriber(ILogger logger, IEngineConfigProvider configProvider, IOptions<MqttEngineOptions> options)
    {
        _logger = logger;
        _configProvider = configProvider;
        _options = options.Value;
    }

    public async Task StartAsync(string domain, string engineId, CancellationToken ct = default)
    {
        var host = ResolveMqttHost();
        if (string.IsNullOrEmpty(host))
        {
            _logger.Information("MQTT Host yapılandırılmamış, MQTT subscribe atlanıyor");
            return;
        }

        var port = ResolveMqttPort();
        var userName = ResolveMqttUserName();
        var password = ResolveMqttPassword();

        var factory = new MqttFactory();
        _client = factory.CreateMqttClient();

        var builder = new MqttClientOptionsBuilder()
            .WithClientId($"mngengine-{engineId}-{Guid.NewGuid():N}")
            .WithTcpServer(host, port)
            .WithCleanSession();

        if (!string.IsNullOrEmpty(userName))
            builder.WithCredentials(userName, password ?? "");

        _clientOptions = builder.Build();

        _client.ApplicationMessageReceivedAsync += e =>
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
            _logger.Debug("MQTT mesaj alındı: {Topic} -> {Payload}", topic, payload);

            if (topic.EndsWith("/sync", StringComparison.OrdinalIgnoreCase))
            {
                SyncRequested?.Invoke(this, EventArgs.Empty);
            }
            else if (topic.EndsWith("/command", StringComparison.OrdinalIgnoreCase))
            {
                var command = ParseCommand(payload);
                if (!string.IsNullOrEmpty(command))
                {
                    CommandReceived?.Invoke(this, new MqttCommandReceivedEventArgs { Command = command, RawPayload = payload });
                    if (string.Equals(command, "sync", StringComparison.OrdinalIgnoreCase))
                        SyncRequested?.Invoke(this, EventArgs.Empty);
                }
            }
            return Task.CompletedTask;
        };

        _client.ConnectedAsync += e =>
        {
            _logger.Information("MQTT broker'a bağlandı");
            return Task.CompletedTask;
        };

        _client.DisconnectedAsync += async e =>
        {
            _logger.Warning("MQTT bağlantısı kesildi: {Reason}. Yeniden bağlanılıyor...", e.Reason);
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            try
            {
                if (_client != null && _clientOptions != null)
                    await _client.ConnectAsync(_clientOptions, ct);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "MQTT yeniden bağlanma hatası");
            }
        };

        try
        {
            await _client.ConnectAsync(_clientOptions, ct);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "MQTT bağlantı hatası");
            return;
        }

        var syncTopic = string.Format(SyncTopicTemplate, domain, engineId);
        var commandTopic = string.Format(CommandTopicTemplate, domain, engineId);

        var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(syncTopic)
            .WithTopicFilter(commandTopic)
            .Build();

        await _client.SubscribeAsync(subscribeOptions, ct);

        _logger.Information("MQTT topic'lere abone olundu: {SyncTopic}, {CommandTopic}", syncTopic, commandTopic);
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_client != null && _client.IsConnected)
        {
            await _client.DisconnectAsync(cancellationToken: ct);
            _logger.Information("MQTT bağlantısı kapatıldı");
        }
    }

    private string? ResolveMqttHost()
    {
        if (!string.IsNullOrEmpty(_options.Host)) return _options.Host;
        var mqttUrl = GetMqttUrlFromConfig();
        if (string.IsNullOrEmpty(mqttUrl)) return null;
        var withoutScheme = mqttUrl.Replace("mqtt://", "", StringComparison.OrdinalIgnoreCase)
            .Replace("mqtts://", "", StringComparison.OrdinalIgnoreCase).TrimStart('/');
        var colon = withoutScheme.IndexOf(':');
        return colon > 0 ? withoutScheme[..colon] : withoutScheme;
    }

    private int ResolveMqttPort()
    {
        if (_options.Port > 0) return _options.Port;
        var mqttUrl = GetMqttUrlFromConfig();
        if (string.IsNullOrEmpty(mqttUrl) || !mqttUrl.Contains(':')) return 1883;
        var parts = mqttUrl.Split(':');
        if (parts.Length >= 3 && int.TryParse(parts[^1].TrimEnd('/'), out var port))
            return port;
        return 1883;
    }

    private string? GetMqttUrlFromConfig()
    {
        var config = _configProvider.GetConfig();
        return config?.MqttUrl;
    }

    private string? ResolveMqttUserName() => !string.IsNullOrEmpty(_options.UserName) ? _options.UserName : null;
    private string? ResolveMqttPassword() => !string.IsNullOrEmpty(_options.Password) ? _options.Password : null;

    /// <summary>
    /// Payload'tan komut adını çıkarır. {"command":"sync"} veya "sync" formatlarını destekler.
    /// </summary>
    private static string? ParseCommand(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return null;
        var trimmed = payload.Trim();
        if (trimmed.StartsWith('{'))
        {
            try
            {
                var doc = JsonDocument.Parse(trimmed);
                if (doc.RootElement.TryGetProperty("command", out var cmd))
                    return cmd.GetString();
            }
            catch { /* ignore */ }
        }
        return trimmed;
    }
}

public class MqttEngineOptions
{
    public const string SectionName = "MngEngine:Mqtt";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 1883;
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
}
