namespace MngAlarm.Application.Configuration;

public class MngAlarmSettings
{
    public const string SectionName = "MngAlarmSettings";

    public MongoDbSettings MongoDb { get; set; } = new();
    public RabbitMqSettings RabbitMq { get; set; } = new();
    public EngineSettings Engine { get; set; } = new();
    public NotificationDispatchSettings NotificationDispatch { get; set; } = new();
}

public class NotificationDispatchSettings
{
    public bool Enabled { get; set; }

    /// <summary>Doluysa Keeper service account yerine bu Bearer token kullanilir.</summary>
    public string? StaticServiceToken { get; set; }

    public DispatchServiceAccountSettings ServiceAccount { get; set; } = new();
    public string MngKeeperBaseUrl { get; set; } = "http://mngkeeper:5001";
    public DispatchDataGatewaySettings DataGateway { get; set; } = new();
    public DispatchHubSettings MngHub { get; set; } = new();
    public DispatchNotifiersSettings MngNotifiers { get; set; } = new();
}

public class DispatchServiceAccountSettings
{
    public string DomainName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class DispatchDataGatewaySettings
{
    public string BaseUrl { get; set; } = "http://mngdatagateway:5010";
    public string ApiVersion { get; set; } = "v1";
}

public class DispatchHubSettings
{
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "http://mnghub:5020";
    public string ApiVersion { get; set; } = "v1";
    public string? InternalNotifyApiKey { get; set; }
}

public class DispatchNotifiersSettings
{
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "http://mngnotifier:5070";
    public string ApiVersion { get; set; } = "v1";
}

public class MongoDbSettings
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string DatabasePrefix { get; set; } = "mng_";
}

public class RabbitMqSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
}

public class EngineSettings
{
    public ushort ObservationPrefetch { get; set; } = 16;

    /// <summary>
    /// Ayni alarm icin ard arda alarm.updated MQ publish minimum araligi (saniye).
    /// 0 = her guncelleme publish edilir. Workflow fan-out korumasi icin varsayilan 5.
    /// </summary>
    public int UpdatedPublishMinIntervalSeconds { get; set; } = 5;

    public bool ConsumeObservations { get; set; } = true;
    public ReactorBridgeSettings ReactorBridge { get; set; } = new();
    public ScenarioDueEvaluationSettings ScenarioDueEvaluation { get; set; } = new();
}

public class ScenarioDueEvaluationSettings
{
    public bool Enabled { get; set; } = true;
    public int ScanIntervalSeconds { get; set; } = 5;
    public int ClaimLeaseSeconds { get; set; } = 30;
    public int BatchSize { get; set; } = 100;
    public int RetryDelaySeconds { get; set; } = 10;
}

public class ReactorBridgeSettings
{
    /// <summary>When true, consume MngReactor metric inserts from mng.topics and republish to monitra.observations.</summary>
    public bool Enabled { get; set; } = true;

    public string Exchange { get; set; } = "mng.topics";
    public string RoutingPattern { get; set; } = "monitoring.metric.inserted.#";
    public ushort Prefetch { get; set; } = 32;
}
