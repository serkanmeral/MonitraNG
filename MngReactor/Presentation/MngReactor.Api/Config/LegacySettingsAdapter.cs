using MngReactor.Application.Configuration;
using MngReactor.Persistence.Settings;

namespace MngReactor.Api.Config;

/// <summary>
/// Eski Persistence/Infrastructure servisleri için geçiş dönemi adapter.
/// </summary>
public static class LegacySettingsAdapter
{
    public static MngReactor.Persistence.Settings.MngReactorSettings ToLegacy(this MngReactor.Application.Configuration.MngReactorSettings source)
    {
        return new MngReactor.Persistence.Settings.MngReactorSettings
        {
            MongoPath = new Mongopath
            {
                host = source.MongoDB?.Host ?? string.Empty,
                port = source.MongoDB?.Port ?? 27017,
                username = source.MongoDB?.Username ?? string.Empty,
                password = source.MongoDB?.Password ?? string.Empty
            },
            MqttSettings = new MngReactor.Persistence.Settings.MqttSettings
            {
                Host = source.Mqtt.Host,
                Port = source.Mqtt.Port,
                UserName = source.Mqtt.UserName,
                Password = source.Mqtt.Password
            },
            TokenService = source.Actors.MngKeeper,
            CompressPrk = source.Crypt.IngestDecryptKey,
            CompressPbk = source.Crypt.IngestEncryptKey,
            ApplicationPort = source.Server.Port,
            MetricsTtlDays = source.Monitoring.MetricsTtlDays,
            SeqPath = "http://localhost:5341",
            ClientName = "reactor",
            Password = string.Empty
        };
    }
}
