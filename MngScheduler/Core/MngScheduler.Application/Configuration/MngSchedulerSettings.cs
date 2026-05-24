namespace MngScheduler.Application.Configuration;

public class MngSchedulerSettings
{
    public ServerSettings Server { get; set; } = new();
    public Mongodb MongoDB { get; set; } = new();
    public Rabbitmq RabbitMQ { get; set; } = new();
    public CertificateSettings CertificateSettings { get; set; } = new();
    public string OpenApiServerPath { get; set; } = string.Empty;
    public Actors Actors { get; set; } = new();
    public DataGatewaySettings DataGateway { get; set; } = new();
    public JobSyncSettings JobSync { get; set; } = new();
    public QuartzSettings Quartz { get; set; } = new();
    public HttpClientSettings HttpClient { get; set; } = new();
    public DirectorySyncOrchestrationSettings DirectorySyncOrchestration { get; set; } = new();
}

public class ServerSettings
{
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 5080;
    public string Scheme { get; set; } = "https";
}

public class CertificateSettings
{
    public string DNS { get; set; } = string.Empty;
    public string MNG_CERT_FILE { get; set; } = string.Empty;
    public string MNG_KEY_FILE { get; set; } = string.Empty;
    public string MNG_CERT_FILE_CONTENT { get; set; } = string.Empty;
    public string MNG_KEY_FILE_CONTENT { get; set; } = string.Empty;
}

public class Actors
{
    public string MngKeeper { get; set; } = string.Empty;
}

public class Mongodb
{
    public string ConnectionString { get; set; } = string.Empty;
    
    /// <summary>
    /// MngKeeper database name (default: "mngkeeper")
    /// Used for System Job storage
    /// </summary>
    public string KeeperDatabaseName { get; set; } = "mngkeeper";
}

public class Rabbitmq
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string VirtualHost { get; set; } = "/";
}

public class DataGatewaySettings
{
    /// <summary>
    /// MngDataGateway base URL
    /// </summary>
    public string BaseUrl { get; set; } = "http://mngdatagateway:5070";
    
    /// <summary>
    /// API version
    /// </summary>
    public string ApiVersion { get; set; } = "v1";
}

public class JobSyncSettings
{
    /// <summary>
    /// Sync interval in seconds (default: 30)
    /// </summary>
    public int SyncIntervalSeconds { get; set; } = 30;
    
    /// <summary>
    /// Enable job sync service
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Poll domain user jobs from MngDataGateway (@scheduled_jobs).
    /// Requires a service JWT; when false, only system jobs (Mongo mngkeeper) are synced.
    /// </summary>
    public bool SyncUserJobs { get; set; } = false;
}

public class QuartzSettings
{
    /// <summary>
    /// Scheduler name
    /// </summary>
    public string SchedulerName { get; set; } = "MngScheduler";
    
    /// <summary>
    /// Thread pool settings
    /// </summary>
    public ThreadPoolSettings ThreadPool { get; set; } = new();
}

public class ThreadPoolSettings
{
    /// <summary>
    /// Thread count (default: 10)
    /// </summary>
    public int ThreadCount { get; set; } = 10;
}

public class HttpClientSettings
{
    /// <summary>
    /// Timeout in seconds (default: 300)
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;
    
    /// <summary>
    /// Maximum retries (default: 3)
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}
