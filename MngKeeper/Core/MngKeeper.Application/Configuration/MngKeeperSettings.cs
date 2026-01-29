namespace MngKeeper.Application.Configuration;

public class MngKeeperSettings
{
    public ServerSettings Server { get; set; } = null!;
    public MongoDbSettings MongoDB { get; set; } = null!;
    public RabbitMqSettings RabbitMQ { get; set; } = null!;
    public RedisSettings Redis { get; set; } = null!;
    public MqttSettings Mqtt { get; set; } = null!;
    public KeycloakSettings Keycloak { get; set; } = null!;
    public MinIOSettings MinIO { get; set; } = null!;
    public NotifierSettings Notifier { get; set; } = null!;
    public CertificateSettings CertificateSettings { get; set; } = null!;
    public LicenseSettings License { get; set; } = new();
    public string OpenApiServerPath { get; set; } = null!;
    /// <summary>
    /// UI uygulamasının base URL'i (şifre sıfırlama mailindeki link için).
    /// Örnek: https://app.monitrang.com (production), http://localhost:3000 (development).
    /// </summary>
    public string? UiBaseUrl { get; set; }
}

public class ServerSettings
{
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 5001;
    public string Scheme { get; set; } = "https";
}

public class MongoDbSettings
{
    public string ConnectionString { get; set; } = null!;
    public string DatabaseName { get; set; } = "mngkeeper";
}

public class RabbitMqSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string VirtualHost { get; set; } = "/";
}

public class RedisSettings
{
    public string ConnectionString { get; set; } = null!;
}

public class MqttSettings
{
    public string BrokerHost { get; set; } = "localhost";
    public int BrokerPort { get; set; } = 1883;
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string TopicPrefix { get; set; } = "MNG";
}

public class KeycloakSettings
{
    public string BaseUrl { get; set; } = null!;
    public string AdminUsername { get; set; } = null!;
    public string AdminPassword { get; set; } = null!;
    public string DefaultAdminPassword { get; set; } = "Admin123!";
}

public class MinIOSettings
{
    public string Endpoint { get; set; } = "localhost:9000";
    public string AccessKey { get; set; } = null!;
    public string SecretKey { get; set; } = null!;
    public bool UseSSL { get; set; } = false;
    public string Region { get; set; } = "us-east-1";
    /// <summary>
    /// Base folder path for system files in MinIO (e.g., "System" for system/System/ structure)
    /// </summary>
    public string SystemFolderPath { get; set; } = "System";
}

public class NotifierSettings
{
    public string BaseUrl { get; set; } = "http://mngnotifier:5070";
    public string ApiVersion { get; set; } = "v1";
}

public class CertificateSettings
{
    public string DNS { get; set; } = null!;
    public string MNG_CERT_FILE { get; set; } = null!;
    public string MNG_KEY_FILE { get; set; } = null!;
    public string MNG_CERT_FILE_CONTENT { get; set; } = null!;
    public string MNG_KEY_FILE_CONTENT { get; set; } = null!;
}

public class LicenseSettings
{
    /// <summary>
    /// Master key for license encryption (should be stored securely in environment variable)
    /// </summary>
    public string MasterKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Default trial license duration in days
    /// </summary>
    public int DefaultTrialDays { get; set; } = 15;
}

