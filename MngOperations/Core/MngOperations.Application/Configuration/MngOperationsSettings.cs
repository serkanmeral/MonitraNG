namespace MngOperations.Application.Configuration;

public class MngOperationsSettings
{
    public const string SectionName = "MngOperationsSettings";

    public ServerSettings Server { get; set; } = new();
    public string OpenApiServerPath { get; set; } = string.Empty;

    /// <summary>
    /// GEÇİCİ (perf/oc-optimization): açıkken board-list/profile endpoint'leri sonunda
    /// OC_PERF log satırı (DG/Keeper çağrı sayısı + süre) yazılır. Default kapalı.
    /// </summary>
    public bool PerfDiagnostics { get; set; } = false;

    public ActorsSettings Actors { get; set; } = new();
    public DataGatewaySettings DataGateway { get; set; } = new();
    public MngNotifiersSettings MngNotifiers { get; set; } = new();
    public RabbitMqSettings RabbitMq { get; set; } = new();
    public MetadataCacheSettings MetadataCache { get; set; } = new();
    public WorkItemScheduleSettings WorkItemSchedule { get; set; } = new();
}

public class ServerSettings
{
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 5086;
    public string Scheme { get; set; } = "http";
}

public class ActorsSettings
{
    public string MngKeeper { get; set; } = string.Empty;
    public string KeycloakBaseUrl { get; set; } = string.Empty;
    public string MngScheduler { get; set; } = string.Empty;
}

public class DataGatewaySettings
{
    public string BaseUrl { get; set; } = "http://mngdatagateway:5010";
    public string ApiVersion { get; set; } = "v1";

    /// <summary>Tek bir DG HTTP çağrısı için üst sınır. İç ağda metadata/query çağrıları
    /// saniyeler sürer; asılı kalan bağlantı 120sn yerine burada patlar.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>5xx/ağ hatasında yeniden deneme sayısı (Polly).</summary>
    public int RetryCount { get; set; } = 3;

    /// <summary>Yeniden denemede üstel backoff taban gecikmesi (ms): delay = Base * 2^(attempt-1).
    /// İç ağ için ms seviyesi yeterli (200 → 200/400/800ms, toplam ~1.4sn). Eski sn-seviyesi backoff
    /// (2^attempt sn = 2+4+8 = 14sn) tek başarısız çağrıda profili 14sn bloke ediyordu.</summary>
    public int RetryBaseDelayMs { get; set; } = 200;
}

public class MngNotifiersSettings
{
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = "http://mngnotifier:5070";
    public string ApiVersion { get; set; } = "v1";
    /// <summary>Username → email dönüşümü için opsiyonel suffix (ör. company.com).</summary>
    public string? EmailDomainSuffix { get; set; }
}

public class RabbitMqSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = "admin123";
    public string VirtualHost { get; set; } = "/";
    public string Exchange { get; set; } = "oc.events";
}

public class MetadataCacheSettings
{
    public int TtlSeconds { get; set; } = 120;

    /// <summary>Global katalog listeleri (nadir değişir) için ayrı, daha uzun TTL.</summary>
    public int CatalogTtlSeconds { get; set; } = 300;

    /// <summary>Keeper kişi (person) çözümlemeleri için TTL — id→ad cache'i.</summary>
    public int PersonTtlSeconds { get; set; } = 300;
}

/// <summary>
/// Zamanlanmış WI → MngScheduler User Job senkronu (SW-3b).
/// </summary>
public class WorkItemScheduleSettings
{
    /// <summary>
    /// HttpJob tetik URL şablonu. {scheduleId} yer tutucusu zorunlu.
    /// SW-2 gelene kadar endpoint 404/501 dönebilir — cron yine planlanır.
    /// </summary>
    public string ExecuteEndpointTemplate { get; set; } =
        "http://mngoperations:5086/api/v1/work-item-schedules/{scheduleId}/execute";

    public string SchedulerJobIdPrefix { get; set; } = "oc-schedule-";
}
