using System.Security.Claims;

namespace MngHub.Infrastructure.Helpers;

/// <summary>
/// Helper class for extracting claims from JWT token claims dictionary
/// </summary>
public static class ClaimsHelper
{
    /// <summary>
    /// Extract domain name from claims dictionary
    /// </summary>
    public static string? GetDomainName(Dictionary<string, string> claims)
    {
        return claims.GetValueOrDefault("domain_name");
    }

    /// <summary>
    /// Extract domain ID (MongoDB ObjectId) from claims dictionary
    /// </summary>
    public static string? GetDomainId(Dictionary<string, string> claims)
    {
        return claims.GetValueOrDefault("domain_id");
    }

    /// <summary>
    /// Extract user ID from claims dictionary
    /// Tries "sub" claim first, then falls back to NameIdentifier
    /// </summary>
    public static string? GetUserId(Dictionary<string, string> claims)
    {
        return claims.GetValueOrDefault("sub") 
            ?? claims.GetValueOrDefault(ClaimTypes.NameIdentifier);
    }

    /// <summary>
    /// Extract username from claims dictionary
    /// Tries "preferred_username" claim first, then falls back to "username"
    /// </summary>
    public static string? GetUsername(Dictionary<string, string> claims)
    {
        return claims.GetValueOrDefault("preferred_username") 
            ?? claims.GetValueOrDefault("username");
    }

    /// <summary>
    /// Validate that required claims exist
    /// </summary>
    /// <param name="claims">Claims dictionary</param>
    /// <param name="domainName">Extracted domain name (output)</param>
    /// <param name="userId">Extracted user ID (output)</param>
    /// <returns>True if all required claims are present</returns>
    public static bool TryExtractRequiredClaims(
        Dictionary<string, string> claims,
        out string? domainName,
        out string? userId)
    {
        domainName = GetDomainName(claims);
        userId = GetUserId(claims);

        return !string.IsNullOrEmpty(domainName) && !string.IsNullOrEmpty(userId);
    }
}

