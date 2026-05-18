using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngKeeper.Application.Configuration;
using MngKeeper.Application.Interfaces;

namespace MngKeeper.Application.Services;

public class TokenCredentialResolver : ITokenCredentialResolver
{
    private readonly IDomainRepository _domainRepository;
    private readonly MngKeeperSettings _settings;
    private readonly ILogger<TokenCredentialResolver> _logger;

    public TokenCredentialResolver(
        IDomainRepository domainRepository,
        IOptions<MngKeeperSettings> settings,
        ILogger<TokenCredentialResolver> logger)
    {
        _domainRepository = domainRepository;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<TokenCredentialResolutionResult> ResolveAsync(
        string username,
        string? domainName = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return TokenCredentialResolutionResult.Fail(
                "invalid_request",
                "Username is required");
        }

        if (!string.IsNullOrWhiteSpace(domainName))
        {
            _logger.LogDebug(
                "Using explicit domain {Domain} for username {Username}",
                domainName,
                username);
            return TokenCredentialResolutionResult.Ok(domainName.Trim(), username);
        }

        if (_settings.Tenant.UseSingleTenant)
        {
            _logger.LogInformation(
                "Single-tenant mode: resolving domain from database without parsing username {Username}",
                username);
            return await ResolveFromSingleDomainAsync(username, cancellationToken);
        }

        var parts = username.Split('@', 2);
        if (parts.Length == 2)
        {
            _logger.LogInformation(
                "Parsed multitenant username: domain={Domain}, username={Username}",
                parts[0],
                parts[1]);
            return TokenCredentialResolutionResult.Ok(parts[0], parts[1]);
        }

        return await ResolveFromSingleDomainAsync(username, cancellationToken);
    }

    private async Task<TokenCredentialResolutionResult> ResolveFromSingleDomainAsync(
        string username,
        CancellationToken cancellationToken)
    {
        var allDomains = await _domainRepository.GetAllAsync();
        var domainList = allDomains.ToList();

        if (domainList.Count == 1)
        {
            _logger.LogInformation(
                "Using single domain {Domain} for username {Username}",
                domainList[0].Name,
                username);
            return TokenCredentialResolutionResult.Ok(domainList[0].Name, username);
        }

        if (domainList.Count == 0)
        {
            return TokenCredentialResolutionResult.Fail(
                "no_domains",
                "No domains found in the system");
        }

        var hint = _settings.Tenant.UseSingleTenant
            ? "Single-tenant mode is enabled but multiple domains exist. Configure one domain or disable UseSingleTenant."
            : "Either provide 'domain' parameter or use 'domain@username' format";

        return TokenCredentialResolutionResult.Fail(
            "domain_required",
            $"Multiple domains found ({domainList.Count}). Domain is required. {hint}");
    }
}
