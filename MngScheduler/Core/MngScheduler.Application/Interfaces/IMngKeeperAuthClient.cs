namespace MngScheduler.Application.Interfaces;

/// <summary>
/// MngKeeper password grant — Operation Core scheduler tetikleri için access token.
/// </summary>
public interface IMngKeeperAuthClient
{
    Task<MngKeeperAccessTokenResult> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}

public class MngKeeperAccessTokenResult
{
    public bool Success { get; set; }

    public string? AccessToken { get; set; }

    public int? ExpiresInSeconds { get; set; }

    public int HttpStatusCode { get; set; }

    public string? ErrorMessage { get; set; }
}
