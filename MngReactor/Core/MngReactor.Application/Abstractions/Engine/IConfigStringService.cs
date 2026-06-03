namespace MngReactor.Application.Abstractions.Engine;

/// <summary>
/// Engine config string üretimi - şifrele + sıkıştır + Base64.
/// </summary>
public interface IConfigStringService
{
    Task<string?> CreateConfigStringAsync(string engineId, string domain, string? accessToken, CancellationToken cancellationToken = default);
}
