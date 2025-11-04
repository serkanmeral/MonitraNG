namespace MngKeeper.Application.Configuration;

public class MngKeeperSettings
{
    public MongoDbSettings MongoDB { get; set; } = null!;
    public RabbitMqSettings RabbitMQ { get; set; } = null!;
    public RedisSettings Redis { get; set; } = null!;
    public MqttSettings Mqtt { get; set; } = null!;
    public KeycloakSettings Keycloak { get; set; } = null!;
    public CertificateSettings CertificateSettings { get; set; } = null!;
    public string OpenApiServerPath { get; set; } = null!;
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

public class CertificateSettings
{
    public string DNS { get; set; } = null!;
    public string MNG_CERT_FILE { get; set; } = null!;
    public string MNG_KEY_FILE { get; set; } = null!;
    public string MNG_CERT_FILE_CONTENT { get; set; } = null!;
    public string MNG_KEY_FILE_CONTENT { get; set; } = null!;
}

