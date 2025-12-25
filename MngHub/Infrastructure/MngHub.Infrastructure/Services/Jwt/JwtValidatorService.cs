using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngHub.Application.Configuration;
using MngHub.Application.Services;
using MngHub.Domain.Exceptions;

namespace MngHub.Infrastructure.Services.Jwt;

public class JwtValidatorService : IJwtValidator
{
    private readonly ILogger<JwtValidatorService> _logger;
    private readonly MngHubSettings _settings;
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;

    public JwtValidatorService(
        ILogger<JwtValidatorService> logger,
        IOptions<MngHubSettings> settings,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache)
    {
        _logger = logger;
        _settings = settings.Value;
        _httpClient = httpClientFactory.CreateClient();
        _cache = cache;
    }

    public async Task<Dictionary<string, string>> ValidateAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new JwtValidationException("Token cannot be empty");
        }

        // Check cache first (5 minutes TTL)
        var cacheKey = $"jwt_{token.GetHashCode()}";
        if (_cache.TryGetValue(cacheKey, out Dictionary<string, string>? cachedClaims))
        {
            _logger.LogDebug("JWT token validated from cache");
            return cachedClaims!;
        }

        try
        {
            // Parse JWT token to extract claims
            var handler = new JwtSecurityTokenHandler();
            
            if (!handler.CanReadToken(token))
            {
                throw new JwtValidationException("Invalid token format");
            }

            var jsonToken = handler.ReadJwtToken(token);

            // Extract claims
            var claims = new Dictionary<string, string>();
            foreach (var claim in jsonToken.Claims)
            {
                claims[claim.Type] = claim.Value;
            }

            // Fallback: Extract domain_name from issuer (realm name) if not present
            // Format: http://localhost:8080/realms/{realm-name} or http://localhost:8080/realms/{domain-name}
            if (!claims.ContainsKey("domain_name"))
            {
                if (claims.TryGetValue("iss", out var issuer))
                {
                    // Extract realm name from issuer URL
                    // Example: http://localhost:8080/realms/test-domain-202512234 -> test-domain-202512234
                    var realmMatch = Regex.Match(issuer, @"/realms/([^/]+)");
                    if (realmMatch.Success && realmMatch.Groups.Count > 1)
                    {
                        var realmName = realmMatch.Groups[1].Value;
                        claims["domain_name"] = realmName;
                        _logger.LogInformation("Extracted domain_name from issuer (realm name): {DomainName}", realmName);
                    }
                }
                
                // If still not found, try to extract from preferred_username
                if (!claims.ContainsKey("domain_name") && claims.TryGetValue("preferred_username", out var preferredUsername))
                {
                    // Format: {domain-name}_admin -> {domain-name}
                    var usernameMatch = Regex.Match(preferredUsername, @"^([^_]+)_");
                    if (usernameMatch.Success && usernameMatch.Groups.Count > 1)
                    {
                        var domainFromUsername = usernameMatch.Groups[1].Value;
                        claims["domain_name"] = domainFromUsername;
                        _logger.LogInformation("Extracted domain_name from preferred_username: {DomainName}", domainFromUsername);
                    }
                }
            }

            // Validate required claims
            if (!claims.ContainsKey("domain_name"))
            {
                throw new JwtValidationException("Token missing required claim: domain_name (and could not extract from issuer or username)");
            }

            if (!claims.ContainsKey("sub") && !claims.ContainsKey(ClaimTypes.NameIdentifier))
            {
                throw new JwtValidationException("Token missing required claim: sub or NameIdentifier");
            }

            // Optional: Validate via MngKeeper API (if needed)
            // For now, we trust the JWT token structure
            // In production, you might want to validate with MngKeeper:
            // var response = await _httpClient.GetAsync($"{_settings.Actors.MngKeeper}/api/auth/validate?token={token}");

            // Cache for 5 minutes
            _cache.Set(cacheKey, claims, TimeSpan.FromMinutes(5));

            _logger.LogDebug("JWT token validated successfully for domain: {Domain}",
                claims.GetValueOrDefault("domain_name"));

            return claims;
        }
        catch (JwtValidationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JWT validation failed");
            throw new JwtValidationException("JWT validation failed", ex);
        }
    }

    public async Task<bool> IsValidAsync(string token)
    {
        try
        {
            await ValidateAsync(token);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

