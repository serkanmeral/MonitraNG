namespace MngReactor.Application.Configuration;

/// <summary>
/// MngReactor application settings (Monitoring planına uyumlu)
/// </summary>
public class MngReactorSettings
{
    public ServerSettings Server { get; set; } = new();
    public string OpenApiServerPath { get; set; } = string.Empty;
    /// <summary>
    /// Config string içindeki serverUrl için. Engine host'ta çalışıyorsa Reactor Gateway arkasında;
    /// burada https://localhost:5040/reactor verin. Boş ise OpenApiServerPath kullanılır.
    /// </summary>
    public string EngineServerUrl { get; set; } = string.Empty;
    public ActorsSettings Actors { get; set; } = new();
    public MongodbSettings MongoDB { get; set; } = new();
    public RabbitmqSettings RabbitMQ { get; set; } = new();
    public DataGatewaySettings DataGateway { get; set; } = new();
    public MqttSettings Mqtt { get; set; } = new();
    public MonitoringSettings Monitoring { get; set; } = new();
    public CryptSettings Crypt { get; set; } = new();
}

public class ServerSettings
{
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 5020;
    public string Scheme { get; set; } = "http";
}

public class ActorsSettings
{
    /// <summary>
    /// MngKeeper URL (JWT doğrulama için authority)
    /// </summary>
    public string MngKeeper { get; set; } = string.Empty;

    /// <summary>
    /// Engine'ın token alabileceği Keeper URL (config string içindeki tokenUrl için).
    /// Engine host'ta çalışıyorsa mngkeeper erişilemez; burada localhost:5001 verin.
    /// Boş ise MngKeeper kullanılır (Engine Docker içindeyse mngkeeper:5001 uygun olur).
    /// </summary>
    public string MngKeeperEngineUrl { get; set; } = string.Empty;
}

public class MongodbSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    /// <summary>
    /// Eski servisler için (geçiş dönemi). ConnectionString varsa öncelikli.
    /// </summary>
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 27017;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RabbitmqSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string VirtualHost { get; set; } = "/";
}

public class DataGatewaySettings
{
    public string BaseUrl { get; set; } = "http://mngdatagateway:5010";
    public string ApiVersion { get; set; } = "v1";
    /// <summary>
    /// Domain bazlı service token'ları (background servisler için, örn: "seven" -> "eyJ...")
    /// Token ilgili domain için geçerli JWT olmalı (domain_name claim).
    /// </summary>
    public Dictionary<string, string> DomainTokens { get; set; } = new();
}

public class MqttSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 1883;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class MonitoringSettings
{
    /// <summary>
    /// mon_metrics Time Series TTL (gün). Varsayılan: 90
    /// </summary>
    public int MetricsTtlDays { get; set; } = 90;

    /// <summary>
    /// Ingest sonrası "data.updated" bildirimi için domain bazlı throttle süresi (saniye).
    /// Aynı domain için bu süre dolmadan tekrar publish yapılmaz. Varsayılan: 5
    /// </summary>
    public int IngestNotifyThrottleSeconds { get; set; } = 5;

    /// <summary>
    /// Ingest sonrası "data.updated" RabbitMQ bildirimi açık mı. false ise hiç gönderilmez. Varsayılan: true
    /// </summary>
    public bool IngestNotifyEnabled { get; set; } = true;
}

public class CryptSettings
{
    /// <summary>
    /// Ingest payload decrypt için private key (Base64 veya hex)
    /// </summary>
    public string IngestDecryptKey { get; set; } = string.Empty;
    /// <summary>
    /// Config string / ingest encrypt için public key
    /// </summary>
    public string IngestEncryptKey { get; set; } = string.Empty;
}
