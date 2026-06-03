using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngReactor.Application.Abstractions.Engine;
using MngReactor.Application.Configuration;
using MngReactor.Domain.Interfaces;

namespace MngReactor.Infrastructure.Services;

public class MqttSyncPublisher : IMqttSyncPublisher
{
    private const string SyncPayload = """{"action":"sync"}""";
    private const string SyncTopicTemplate = "monitoring/{0}/engine/{1}/sync";
    private const string CommandTopicTemplate = "monitoring/{0}/engine/{1}/command";

    private readonly ILogger<MqttSyncPublisher> _logger;
    private readonly IMqttService _mqttService;
    private readonly IEngineIdsForAssetResolver _engineIdsResolver;
    private readonly MqttSettings _mqttSettings;

    public MqttSyncPublisher(
        ILogger<MqttSyncPublisher> logger,
        IMqttService mqttService,
        IEngineIdsForAssetResolver engineIdsResolver,
        IOptions<MngReactorSettings> options)
    {
        _logger = logger;
        _mqttService = mqttService;
        _engineIdsResolver = engineIdsResolver;
        _mqttSettings = options.Value.Mqtt;
    }

    public async Task PublishSyncAsync(string domain, string engineId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_mqttSettings.Host))
        {
            _logger.LogDebug("MQTT Host boş, sync publish atlanıyor");
            return;
        }
        if (string.IsNullOrEmpty(domain) || string.IsNullOrEmpty(engineId))
            return;

        var topic = string.Format(SyncTopicTemplate, domain, engineId);
        try
        {
            await _mqttService.PublishAsync(topic, SyncPayload);
            _logger.LogDebug("MQTT sync yayınlandı: {Topic}", topic);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MQTT sync publish hatası: {Topic}", topic);
        }
    }

    public async Task PublishSyncForAssetAsync(string domain, string assetId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(domain) || string.IsNullOrEmpty(assetId))
            return;

        var engineIds = await _engineIdsResolver.GetEngineIdsForAssetAsync(domain, assetId, accessToken: null, cancellationToken);
        foreach (var engineId in engineIds)
        {
            await PublishSyncAsync(domain, engineId, cancellationToken);
        }
    }

    public async Task PublishCommandAsync(string domain, string engineId, string payload, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_mqttSettings.Host))
        {
            _logger.LogDebug("MQTT Host boş, command publish atlanıyor");
            return;
        }
        if (string.IsNullOrEmpty(domain) || string.IsNullOrEmpty(engineId) || string.IsNullOrEmpty(payload))
            return;

        var topic = string.Format(CommandTopicTemplate, domain, engineId);
        try
        {
            await _mqttService.PublishAsync(topic, payload);
            _logger.LogDebug("MQTT command yayınlandı: {Topic}", topic);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MQTT command publish hatası: {Topic}", topic);
        }
    }
}
