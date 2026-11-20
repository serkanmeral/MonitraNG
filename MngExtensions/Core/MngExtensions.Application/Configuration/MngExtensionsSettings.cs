namespace MngExtensions.Application.Configuration;

public class MngExtensionsSettings
{
    public const string SectionName = "MngExtensionsSettings";

    public ServerSettings Server { get; set; } = new();
    public string OpenApiServerPath { get; set; } = string.Empty;
    public RedisSettings Redis { get; set; } = new();
    public KeeperSettings Keeper { get; set; } = new();
    public DataGatewaySettings DataGateway { get; set; } = new();
    public ServiceAccountSettings ServiceAccount { get; set; } = new();
    public PdksSettings Pdks { get; set; } = new();
}

public class ServerSettings
{
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 5092;
    public string Scheme { get; set; } = "http";
}

public class RedisSettings
{
    public string Host { get; set; } = "redis";
    public int Port { get; set; } = 6379;
    public string Password { get; set; } = string.Empty;
}

public class KeeperSettings
{
    public string BaseUrl { get; set; } = "http://mngkeeper:5001";
    public string TokenPath { get; set; } = "/api/auth/token";
}

public class DataGatewaySettings
{
    public string BaseUrl { get; set; } = "http://mngdatagateway:5010";
    public string ApiVersion { get; set; } = "v1";
}

public class ServiceAccountSettings
{
    public string DomainName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class PdksSettings
{
    public string PublicAppBaseUrl { get; set; } = string.Empty;
    public string AttendanceGroup { get; set; } = string.Empty;
    public int CodeSeconds { get; set; } = 10;
    public int PasswordHoldSeconds { get; set; } = 90;
    public int DuplicateSeconds { get; set; } = 30;
    public int NonceRetentionSeconds { get; set; } = 120;
}
