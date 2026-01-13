namespace MngGateway.Application.Configuration;

public class MngGatewaySettings
{
    public ServerSettings Server { get; set; } = new();
    public JwtSettings Jwt { get; set; } = new();
    public CorsSettings Cors { get; set; } = new();
    public RateLimitSettings RateLimit { get; set; } = new();
    public CertificateSettings CertificateSettings { get; set; } = new();
    public string OpenApiServerPath { get; set; } = string.Empty;
    public BackendServices BackendServices { get; set; } = new();
}

public class ServerSettings
{
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 5000;
    public string Scheme { get; set; } = "https";
}

public class JwtSettings
{
    public string Authority { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public bool RequireHttpsMetadata { get; set; } = false;
}

public class CorsSettings
{
    public List<string> AllowedOrigins { get; set; } = new();
    public bool AllowCredentials { get; set; } = true;
}

public class RateLimitSettings
{
    public bool EnableRateLimiting { get; set; } = true;
    public int AnonymousLimit { get; set; } = 30; // requests per minute
    public int AuthenticatedLimit { get; set; } = 100; // requests per minute
    public int AdminLimit { get; set; } = 500; // requests per minute
    public string Period { get; set; } = "1m";
}

public class CertificateSettings
{
    public string DNS { get; set; } = string.Empty;
    public string MNG_CERT_FILE { get; set; } = string.Empty;
    public string MNG_KEY_FILE { get; set; } = string.Empty;
    public string MNG_CERT_FILE_CONTENT { get; set; } = string.Empty;
    public string MNG_KEY_FILE_CONTENT { get; set; } = string.Empty;
}

public class BackendServices
{
    public string MngKeeper { get; set; } = "http://mngkeeper:5001";
    public string MngDataGateway { get; set; } = "http://mngdatagateway:5010";
    public string MngHub { get; set; } = "http://mnghub:5020";
    public string MngReactor { get; set; } = "http://mngreactor:5003";
    public string MngLLM { get; set; } = "http://mngllm:5030";
    public string MngNotifier { get; set; } = "http://mngnotifier:5070";
    public string MngAdmin { get; set; } = "http://mngadmin:5080";
    public string KeyCloak { get; set; } = "http://keycloak:8080";
}

