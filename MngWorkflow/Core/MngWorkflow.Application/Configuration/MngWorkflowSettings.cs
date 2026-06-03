using MngWorkflow.Domain.Constants;

namespace MngWorkflow.Application.Configuration;

/// <summary>
/// MngWorkflow uygulama ayarları.
/// </summary>
public class MngWorkflowSettings
{
    public const string SectionName = "MngWorkflowSettings";

    public string DataGatewayBaseUrl { get; set; } = "http://localhost:5010";

    public string NotifierBaseUrl { get; set; } = "http://localhost:5070";

    public MongoDbSettings MongoDb { get; set; } = new();

    public RabbitMqSettings RabbitMq { get; set; } = new();

    public EngineSettings Engine { get; set; } = new();

    public SecretSettings Secrets { get; set; } = new();

    public SchedulerSettings Scheduler { get; set; } = new();

    public OperationsSettings Operations { get; set; } = new();

    public EngineCommandSettings EngineCommand { get; set; } = new();

    public List<string> Domains { get; set; } = new();
}

public class MongoDbSettings
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";

    /// <summary>Domain DB prefix — tam ad: mng_{domainName}</summary>
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

/// <summary>Worker runtime tuning — düşük gecikme, kontrollü paralellik.</summary>
public class EngineSettings
{
    /// <summary>RabbitMQ BasicQos prefetch — aynı consumer'da eşzamanlı unacked mesaj üst sınırı.</summary>
    public ushort PrefetchCount { get; set; } = 16;

    /// <summary>HTTP action node timeout (saniye).</summary>
    public int HttpTimeoutSeconds { get; set; } = 30;

    /// <summary>Node execution timeout (saniye).</summary>
    public int NodeTimeoutSeconds { get; set; } = 120;

    public int MaxAttempts { get; set; } = 3;

    /// <summary>Delay node: bu sürenin altı bucket kuyruğu, üstü MngScheduler.</summary>
    public int DelaySchedulerThresholdSeconds { get; set; } = 60;

    public ExpressionSettings Expression { get; set; } = new();

    public EventTriggerSettings EventTrigger { get; set; } = new();
}

public class EventTriggerSettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>oc.events routing pattern (topic).</summary>
    public string OcEventsRoutingPattern { get; set; } = WorkflowEventExchanges.OcWorkItemRoutingPattern;

    /// <summary>mng.alarms routing pattern (topic).</summary>
    public string AlarmsRoutingPattern { get; set; } = WorkflowEventExchanges.AlarmsRoutingPattern;
}

public class ExpressionSettings
{
    /// <summary>Jint execution timeout (ms).</summary>
    public int TimeoutMilliseconds { get; set; } = 500;

    public int MaxStatements { get; set; } = 1000;

    public int MaxRecursionDepth { get; set; } = 32;
}

public class SecretSettings
{
    /// <summary>AES-256 key (base64). Boşsa secret özelliği devre dışı kalır.</summary>
    public string EncryptionKeyBase64 { get; set; } = string.Empty;
}

public class SchedulerSettings
{
    public string BaseUrl { get; set; } = "http://mngscheduler:5090";
    /// <summary>Scheduler HTTP job'larının çağıracağı workflow API tabanı (gateway veya doğrudan servis).</summary>
    public string HookBaseUrl { get; set; } = "http://mngworkflow:5085";
    public string DelayResumePath { get; set; } = "/api/v1/hooks/resume/delay";
    public string ScheduleRunPath { get; set; } = "/api/v1/hooks/schedule/run";
    public string JobIdPrefix { get; set; } = "wf-";
    public ServiceAccountSettings ServiceAccount { get; set; } = new();
    public string MngKeeperBaseUrl { get; set; } = "http://mngkeeper:5001";
}

public class ServiceAccountSettings
{
    public string DomainName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class OperationsSettings
{
    public string BaseUrl { get; set; } = "http://mngoperations:5086";
}

/// <summary>MQTT engine command channel — Reactor /api/v1/mqtt/publish veya dev log-only.</summary>
public class EngineCommandSettings
{
    public string ReactorBaseUrl { get; set; } = "http://mngreactor:5003";

    /// <summary>Opsiyonel MQTT topic prefix (örn. MNG).</summary>
    public string? MqttTopicPrefix { get; set; }

    /// <summary>Reactor ayakta değilken komutu logla ve başarılı say (Odak stub).</summary>
    public bool DevLogOnly { get; set; }

    public string? DefaultEngineId { get; set; }

    public int HttpTimeoutSeconds { get; set; } = 15;
}
