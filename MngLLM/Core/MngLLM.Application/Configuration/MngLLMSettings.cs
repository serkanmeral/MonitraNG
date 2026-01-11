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
}

/// <summary>
/// Server configuration
/// </summary>
public class ServerSettings
{
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 5030;
    public string Scheme { get; set; } = "https";
}

/// <summary>
/// Ollama configuration
/// </summary>
public class OllamaSettings
{
    public string BaseUrl { get; set; } = "http://ollama:11434";
    public string DefaultModel { get; set; } = "qwen2.5:3b";
    public int Timeout { get; set; } = 30;
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
