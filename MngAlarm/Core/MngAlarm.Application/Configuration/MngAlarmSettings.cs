namespace MngAlarm.Application.Configuration;

public class MngAlarmSettings
{
    public const string SectionName = "MngAlarmSettings";

    public MongoDbSettings MongoDb { get; set; } = new();
    public RabbitMqSettings RabbitMq { get; set; } = new();
    public EngineSettings Engine { get; set; } = new();
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
}

public class ReactorBridgeSettings
{
    /// <summary>When true, consume MngReactor metric inserts from mng.topics and republish to monitra.observations.</summary>
    public bool Enabled { get; set; } = true;

    public string Exchange { get; set; } = "mng.topics";
    public string RoutingPattern { get; set; } = "monitoring.metric.inserted.#";
    public ushort Prefetch { get; set; } = 32;
}
