using MngKeeper.Application.Services;

namespace MngKeeper.Application.Interfaces;

public interface ITokenCredentialResolver
{
    Task<TokenCredentialResolutionResult> ResolveAsync(
        string username,
        string? domainName = null,
        CancellationToken cancellationToken = default);
}
