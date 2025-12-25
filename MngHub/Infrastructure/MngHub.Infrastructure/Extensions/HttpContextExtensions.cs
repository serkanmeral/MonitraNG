using Microsoft.AspNetCore.Http;

namespace MngHub.Infrastructure.Extensions;

/// <summary>
/// Extension methods for HttpContext to extract JWT token
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// Extracts JWT token from query string or Authorization header
    /// </summary>
    public static string? ExtractJwtToken(this HttpContext? httpContext)
    {
        if (httpContext == null)
            return null;

        // Try query string first
        var token = httpContext.Request.Query["access_token"].ToString();
        
        // If not in query string, try Authorization header
        if (string.IsNullOrEmpty(token))
        {
            var authHeader = httpContext.Request.Headers["Authorization"].ToString();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = authHeader.Substring("Bearer ".Length).Trim();
            }
        }

        return string.IsNullOrEmpty(token) ? null : token;
    }
}

