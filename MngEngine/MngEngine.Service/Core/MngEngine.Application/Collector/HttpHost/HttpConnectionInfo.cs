namespace MngEngine.Application.Collector.HttpHost;

/// <summary>
/// HTTP/REST collector için bağlantı bilgisi (UI connection_info.baseUrl + auth ile uyumlu).
/// </summary>
public record HttpConnectionInfo
{
    public required string BaseUrl { get; init; }
    /// <summary>none | basic | bearer_token</summary>
    public string AuthType { get; init; } = "none";
    public string? Username { get; init; }
    public string? Password { get; init; }
    /// <summary>Bearer token için mon_http_auth_configs __dataId (ileride token çözümleme).</summary>
    public string? AuthConfigId { get; init; }
}
