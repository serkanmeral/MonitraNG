namespace MngLLM.Application.Services;

public interface ILlmKeeperAuthClient
{
    /// <summary>Returns a Bearer access token for the configured service account.</summary>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}
