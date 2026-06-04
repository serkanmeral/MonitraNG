namespace MngEngine.Application.Interfaces;

/// <summary>
/// Config'teki credential'larla Keeper/Reactor token endpoint'inden access token alır.
/// </summary>
public interface IAccessTokenProvider
{
    Task<string?> GetAccessTokenAsync(CancellationToken ct = default);
}
