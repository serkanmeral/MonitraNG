using System.Buffers;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using MngSim.Models.TrainSim;
using MQTTnet;

namespace MngSim.Services.TrainSim;

public class TrainEventService : ITrainEventService, IAsyncDisposable
{
    private readonly IConfiguration _config;
    private readonly ILogger<TrainEventService> _logger;
    private readonly IMqttClient? _client;
    private readonly string _topicAll = "mngsim/trains/events";
    private readonly int _maxEvents = 150;
    private readonly ConcurrentQueue<TrainEventDto> _recentEvents = new();

    public bool IsConnected => _client?.IsConnected ?? false;

    public TrainEventService(IConfiguration config, ILogger<TrainEventService> logger)
    {
        _config = config;
        _logger = logger;
        var brokerUrl = _config["TrainSim:MqttBrokerUrl"]?.Trim();
        if (string.IsNullOrEmpty(brokerUrl))
        {
            _logger.LogInformation("Tren event MQTT: Broker URL tanımlı değil (TrainSim:MqttBrokerUrl), event yayını devre dışı.");
            _client = null;
            return;
        }

        _client = new MqttClientFactory().CreateMqttClient();
        var userName = _config["TrainSim:MqttUserName"]?.Trim();
        var password = _config["TrainSim:MqttPassword"]?.Trim();
        _ = ConnectAndSubscribeAsync(brokerUrl, userName, password);
    }

    private async Task ConnectAndSubscribeAsync(string brokerUrl, string? userName, string? password)
    {
        if (_client == null) return;
        try
        {
            var uri = new Uri(brokerUrl);
            var port = uri.Port > 0 ? uri.Port : 1883;
            var optionsBuilder = new MqttClientOptionsBuilder()
                .WithTcpServer(uri.Host, port)
                .WithClientId("MngSim-TrainEvents-" + Guid.NewGuid().ToString("N")[..8])
                .WithCleanSession();
            if (!string.IsNullOrEmpty(userName))
                optionsBuilder.WithCredentials(userName, password ?? "");
            var options = optionsBuilder.Build();
            var result = await _client.ConnectAsync(options);
            if (result.ResultCode != MqttClientConnectResultCode.Success)
            {
                _logger.LogWarning("Tren event MQTT: Broker reddetti: {Reason}", result.ResultCode);
                return;
            }
            _logger.LogInformation("Tren event MQTT: Broker'a bağlandı: {Host}:{Port}", uri.Host, port);

            await _client.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic(_topicAll).Build());
            _client.ApplicationMessageReceivedAsync += OnMessageReceived;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tren event MQTT: Broker bağlantısı başarısız: {Url}", brokerUrl);
        }
    }

    private Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            var payload = e.ApplicationMessage.Payload;
            if (payload.Length == 0) return Task.CompletedTask;
            var arr = new byte[(int)payload.Length];
            payload.CopyTo(arr.AsSpan());
            var json = Encoding.UTF8.GetString(arr);
            var dto = JsonSerializer.Deserialize<TrainEventDto>(json);
            if (dto != null)
            {
                Enqueue(dto);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Tren event mesaj parse hatası.");
        }
        return Task.CompletedTask;
    }

    private void Enqueue(TrainEventDto dto)
    {
        _recentEvents.Enqueue(dto);
        while (_recentEvents.Count > _maxEvents && _recentEvents.TryDequeue(out _)) { }
    }

    public async Task PublishAsync(string trainId, TrainEventDto payload, CancellationToken ct = default)
    {
        payload.TrainId = trainId;
        if (payload.Timestamp == default)
            payload.Timestamp = DateTime.UtcNow;

        if (_client != null && _client.IsConnected)
        {
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var msg = new MqttApplicationMessageBuilder()
                .WithTopic(_topicAll)
                .WithPayload(json)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();
            await _client.PublishAsync(msg, ct);
            var perTrainTopic = "mngsim/trains/" + trainId + "/events";
            var msg2 = new MqttApplicationMessageBuilder()
                .WithTopic(perTrainTopic)
                .WithPayload(json)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();
            await _client.PublishAsync(msg2, ct);
            _logger.LogInformation("Tren event yayınlandı: {TrainId} {EventType}", trainId, payload.EventType);
        }
        else
            _logger.LogWarning("Tren event: MQTT bağlı değil, event yalnızca yerel loga yazıldı.");

        Enqueue(payload);
    }

    public IReadOnlyList<TrainEventDto> GetRecentEvents(int maxCount = 100)
    {
        var list = _recentEvents.ToArray();
        if (list.Length <= maxCount) return list;
        return list.Skip(list.Length - maxCount).ToList();
    }

    public async ValueTask DisposeAsync()
    {
        if (_client != null)
        {
            _client.ApplicationMessageReceivedAsync -= OnMessageReceived;
            if (_client.IsConnected)
                await _client.DisconnectAsync();
            _client.Dispose();
        }
    }
}
