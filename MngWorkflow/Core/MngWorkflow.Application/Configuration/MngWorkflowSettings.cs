namespace MngWorkflow.Application.Configuration;

/// <summary>
/// MngWorkflow uygulama ayarları.
/// </summary>
public class MngWorkflowSettings
{
    public const string SectionName = "MngWorkflowSettings";

    /// <summary>
    /// MngDataGateway API base URL (Gateway üzerinden: https://localhost:5040/data veya DG direkt: http://localhost:5010).
    /// </summary>
    public string DataGatewayBaseUrl { get; set; } = "http://localhost:5010";

    /// <summary>
    /// MngNotifier API base URL (mail gönderimi için).
    /// </summary>
    public string NotifierBaseUrl { get; set; } = "http://localhost:5070";

    /// <summary>
    /// RabbitMQ bağlantı ayarları.
    /// </summary>
    public RabbitMqSettings RabbitMq { get; set; } = new();

    /// <summary>
    /// Dinlenecek domain exchange'leri. Boş ise config'ten veya başka kaynaktan alınır.
    /// </summary>
    public List<string> Domains { get; set; } = new();
}

public class RabbitMqSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
}
