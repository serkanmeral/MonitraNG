namespace MngLLM.Application.Configuration;

/// <summary>
/// MngLLM Service Configuration
/// </summary>
public class MngLLMSettings
{
    public ServerSettings Server { get; set; } = new();
    public OllamaSettings Ollama { get; set; } = new();
    public TranslationSettings Translation { get; set; } = new();
    public string OpenApiServerPath { get; set; } = string.Empty;
    public CertificateSettings CertificateSettings { get; set; } = new();
    public ActorsSettings Actors { get; set; } = new();
    public CorsSettings Cors { get; set; } = new();
    public DocumentationSettings Documentation { get; set; } = new();
}

/// <summary>
/// Server configuration
/// </summary>
public class ServerSettings
{
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 5030;
    public string Scheme { get; set; } = "http"; // HTTP - SSL termination at API Gateway
}

/// <summary>
/// Ollama configuration
/// </summary>
public class OllamaSettings
{
    public string BaseUrl { get; set; } = "http://ollama:11434";
    public string DefaultModel { get; set; } = "qwen2.5:3b";
    public int Timeout { get; set; } = 120; // 2 minutes - increased for complex prompts
    public int MaxRetries { get; set; } = 2; // Maximum retry attempts for failed requests
    public bool EnableStreaming { get; set; } = false; // Streaming response (requires frontend support)
}

/// <summary>
/// Translation configuration
/// </summary>
public class TranslationSettings
{
    public List<string> SupportedLanguages { get; set; } = new() { "tr", "en", "fr", "ar", "zh" };
    public bool CacheEnabled { get; set; } = true;
    public int CacheTTL { get; set; } = 3600; // 1 hour
}

/// <summary>
/// Certificate settings
/// </summary>
public class CertificateSettings
{
    public string DNS { get; set; } = string.Empty;
    public string CERT_FILE { get; set; } = string.Empty;
    public string KEY_FILE { get; set; } = string.Empty;
    public string CERT_FILE_CONTENT { get; set; } = string.Empty;
    public string KEY_FILE_CONTENT { get; set; } = string.Empty;
}

/// <summary>
/// External services (actors)
/// </summary>
public class ActorsSettings
{
    public string MngKeeper { get; set; } = string.Empty;
    public string MngDataGateway { get; set; } = string.Empty;
}

/// <summary>
/// CORS configuration
/// </summary>
public class CorsSettings
{
    public List<string> AllowedOrigins { get; set; } = new() { "http://localhost:3000", "https://localhost:3000" };
    public bool AllowCredentials { get; set; } = true;
}

/// <summary>
/// Documentation settings
/// </summary>
public class DocumentationSettings
{
    /// <summary>
    /// Markdown dokümantasyon dosyalarının yolu (relative to MngLLM.Api)
    /// </summary>
    public string MarkdownPath { get; set; } = "../../docs/content";
    
    /// <summary>
    /// Search sonuç limiti (varsayılan: 5)
    /// </summary>
    public int SearchLimit { get; set; } = 5;
    
    /// <summary>
    /// Re-index interval (dakika cinsinden, varsayılan: 60)
    /// </summary>
    public int ReindexIntervalMinutes { get; set; } = 60;
    
    /// <summary>
    /// Otomatik re-indexing aktif mi?
    /// </summary>
    public bool EnableAutoReindex { get; set; } = true;
    
    /// <summary>
    /// Service endpoint'leri (OpenAPI JSON'ları için)
    /// </summary>
    public List<ServiceEndpoint> ServiceEndpoints { get; set; } = new();
}

/// <summary>
/// Service endpoint configuration
/// </summary>
public class ServiceEndpoint
{
    /// <summary>
    /// Service name (örn: "MngDataGateway", "MngKeeper")
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;
    
    /// <summary>
    /// Base URL (örn: "http://mngdatagateway:5010")
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// OpenAPI JSON path (varsayılan: "/api-docs/v1/swagger.json")
    /// </summary>
    public string OpenApiPath { get; set; } = "/api-docs/v1/swagger.json";
}
