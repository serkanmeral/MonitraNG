using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MngKeeper.Application.Interfaces;

namespace MngKeeper.Api.Middleware;

/// <summary>
/// Middleware to extract JWT claims and populate HttpContext
/// </summary>
public class JwtClaimsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<JwtClaimsMiddleware> _logger;

    public JwtClaimsMiddleware(RequestDelegate next, ILogger<JwtClaimsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IDomainRepository domainRepository)
    {
        try
        {
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            
            _logger.LogInformation(">>> [JWT-MW] Processing request: {Path}", context.Request.Path);
            
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            {
                var token = authHeader.Substring("Bearer ".Length).Trim();
                _logger.LogInformation(">>> [JWT-MW] Token found, parsing...");
                
                // Parse JWT token
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);
                
                // Extract claims
                var email = jwtToken.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
                var username = jwtToken.Claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value;
                var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
                var mngPersonId = jwtToken.Claims.FirstOrDefault(c => c.Type == "mng_person_id")?.Value;
                var domainName = jwtToken.Claims.FirstOrDefault(c => c.Type == "domain_name")?.Value;
                var userGroupsClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "user_groups")?.Value;
                var isAdminClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "isAdmin")?.Value;
                
                _logger.LogInformation(">>> [JWT-MW] Claims extracted - User: {Username}, Domain: {DomainName}, IsAdmin: {IsAdmin}", 
                    username, domainName, isAdminClaim);
                
                // Parse user_groups (can be string or array)
                var userGroups = new List<string>();
                if (!string.IsNullOrEmpty(userGroupsClaim))
                {
                    userGroups.Add(userGroupsClaim);
                }
                
                // Parse isAdmin
                var isAdmin = bool.TryParse(isAdminClaim, out var isAdminValue) && isAdminValue;
                
                // Get domain ID from domain name (lookup in database)
                string? domainId = null;
                try
                {
                    if (!string.IsNullOrEmpty(domainName))
                    {
                        _logger.LogInformation(">>> [JWT-MW] Looking up domain: {DomainName}", domainName);
                        var domain = await domainRepository.GetByNameAsync(domainName);
                        
                        if (domain != null)
                        {
                            domainId = domain.Id;
                            _logger.LogInformation(">>> [JWT-MW] Domain found - ID: {DomainId}", domainId);
                        }
                        else
                        {
                            _logger.LogWarning(">>> [JWT-MW] Domain NOT found: {DomainName}", domainName);
                        }
                    }
                }
                catch (Exception domainEx)
                {
                    _logger.LogError(domainEx, ">>> [JWT-MW] Error looking up domain: {DomainName}", domainName);
                }
                
                // Create TokenClaims object
                var tokenClaims = new TokenClaims
                {
                    UserId = userId,
                    MngPersonId = mngPersonId,
                    Email = email,
                    Username = username,
                    DomainId = domainId,
                    DomainName = domainName,
                    Groups = userGroups,
                    IsAdmin = isAdmin
                };
                
                // Store in HttpContext
                context.Items["TokenClaims"] = tokenClaims;
                
                _logger.LogInformation(">>> [JWT-MW] TokenClaims stored - User: {Username}, DomainId: {DomainId}, DomainName: {DomainName}", 
                    username, domainId ?? "NULL", domainName ?? "NULL");
            }
            else
            {
                _logger.LogDebug(">>> [JWT-MW] No Bearer token found");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ">>> [JWT-MW] EXCEPTION while parsing JWT token");
        }
        
        await _next(context);
    }
}

/// <summary>
/// Extension method for adding JWT claims middleware
/// </summary>
public static class JwtClaimsMiddlewareExtensions
{
    public static IApplicationBuilder UseJwtClaims(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<JwtClaimsMiddleware>();
    }
}

