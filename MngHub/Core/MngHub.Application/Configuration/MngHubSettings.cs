namespace MngHub.Application.Configuration;

public class MngHubSettings
{
    public ServerSettings Server { get; set; } = new();
    public Rabbitmq RabbitMQ { get; set; } = new();
    public CertificateSettings CertificateSettings { get; set; } = new();
    public string OpenApiServerPath { get; set; } = string.Empty;
    public Actors Actors { get; set; } = new();
    public SignalRSettings SignalR { get; set; } = new();
    public ConnectionSettings Connection { get; set; } = new();
}

public class ServerSettings
{
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 5020;
    public string Scheme { get; set; } = "https";
}

public class Rabbitmq
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = "admin123";
    public string VirtualHost { get; set; } = "/";
    public string ExchangeName { get; set; } = "mng.topics";
    public string EventPublisherExchangeName { get; set; } = "mngkeeper.events"; // For user/group events
}

public class CertificateSettings
{
    public string DNS { get; set; } = string.Empty;
    public string CERT_FILE { get; set; } = string.Empty;
    public string KEY_FILE { get; set; } = string.Empty;
    public string CERT_FILE_CONTENT { get; set; } = string.Empty;
    public string KEY_FILE_CONTENT { get; set; } = string.Empty;
}

public class Actors
{
    public string MngKeeper { get; set; } = string.Empty;
}

public class SignalRSettings
{
    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan ClientTimeoutInterval { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan HandshakeTimeout { get; set; } = TimeSpan.FromSeconds(15);
    public int MaximumReceiveMessageSize { get; set; } = 32 * 1024; // 32KB
}

public class ConnectionSettings
{
    public int MaxConcurrentConnections { get; set; } = 5000;
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxMessageSize { get; set; } = 32 * 1024; // 32KB
    public int RateLimitPerConnection { get; set; } = 100; // messages/minute
}

