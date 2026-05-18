namespace MngNotifier.Application.Configuration;

public class MngNotifierSettings
{
    public ServerSettings Server { get; set; } = new();
    public RabbitMQSettings RabbitMQ { get; set; } = new();
    public MailSettings Mail { get; set; } = new();
    public DataGatewaySettings DataGateway { get; set; } = new();

    /// <summary>
    /// Boş değilse, <c>POST .../notifications/chat-mention</c> için <c>X-Monitra-Notify-Key</c> başlığı zorunludur (MngDataGateway ile aynı değer).
    /// </summary>
    public string? InternalNotifyApiKey { get; set; }
}

public class ServerSettings
{
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 5070;
    public string Scheme { get; set; } = "http";
}

public class RabbitMQSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = "admin123";
    public string VirtualHost { get; set; } = "/";
}

public class MailSettings
{
    public string Provider { get; set; } = "SMTP";
    public DefaultFromSettings DefaultFrom { get; set; } = new();
    public SmtpSettings Smtp { get; set; } = new();
}

public class DefaultFromSettings
{
    public string Email { get; set; } = "noreply@monitrang.com";
    public string? Name { get; set; } = "MonitraNG";
}

public class SmtpSettings
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 25;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public bool EnableSsl { get; set; } = false;
}

public class DataGatewaySettings
{
    public string BaseUrl { get; set; } = "http://mngdatagateway:5010";
    public string ApiVersion { get; set; } = "v1";
}
