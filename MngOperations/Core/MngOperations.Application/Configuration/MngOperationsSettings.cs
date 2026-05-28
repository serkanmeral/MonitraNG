namespace MngOperations.Application.Configuration;

public class MngOperationsSettings
{
    public const string SectionName = "MngOperationsSettings";

    public ServerSettings Server { get; set; } = new();
    public string OpenApiServerPath { get; set; } = string.Empty;
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
