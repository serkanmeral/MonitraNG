using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace MngDataGateway.Api.Controllers;

/// <summary>
/// Authentication test controller for JWT token validation
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthTestController : ControllerBase
{
    private readonly ILogger<AuthTestController> _logger;

    public AuthTestController(ILogger<AuthTestController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Test endpoint - no authentication required
    /// </summary>
    /// <returns>Simple test message</returns>
    [HttpGet("public")]
    [AllowAnonymous]
    public IActionResult PublicTest()
    {
        return Ok(new
        {
            message = "Public endpoint - No authentication required",
            timestamp = DateTime.UtcNow,
            authenticated = User.Identity?.IsAuthenticated ?? false
        });
    }

    /// <summary>
    /// Decode and return JWT token claims
    /// </summary>
    /// <returns>Decoded token information including domain_name, user info, and all claims</returns>
    /// <response code="200">Returns decoded token information</response>
    /// <response code="401">Unauthorized - No valid token provided</response>
    [HttpGet("decode")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult DecodeToken()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Unauthorized(new { message = "No valid authentication token provided" });
        }

        // Extract important claims
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                     ?? User.FindFirst("sub")?.Value;
        var email = User.FindFirst(ClaimTypes.Email)?.Value 
                    ?? User.FindFirst("email")?.Value;
        var username = User.FindFirst(ClaimTypes.Name)?.Value 
                       ?? User.FindFirst("preferred_username")?.Value;
        
        // Domain information (critical for multi-tenant)
        var domainId = User.FindFirst("domain_id")?.Value;
        var domainName = User.FindFirst("domain_name")?.Value;
        var domainRealm = User.FindFirst("domain_realm")?.Value;
        
        // Admin flag
        var isAdmin = User.FindFirst("is_admin")?.Value;
        
        // Roles
        var roles = User.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        // Get all claims for debugging
        var allClaims = User.Claims
            .Select(c => new { c.Type, c.Value })
            .ToList();

        // Calculate database name (as per MngDataGateway design)
        var databaseName = !string.IsNullOrEmpty(domainName) 
            ? $"mng_{domainName}" 
            : "unknown";

        var response = new
        {
            authenticated = true,
            timestamp = DateTime.UtcNow,
            
            user = new
            {
                id = userId,
                email = email,
                username = username
            },
            
            domain = new
            {
                id = domainId,
                name = domainName,
                realm = domainRealm,
                database = databaseName  // MongoDB database name
            },
            
            authorization = new
            {
                isAdmin = isAdmin,
                roles = roles
            },
            
            claims = allClaims
        };

        _logger.LogInformation(
            "Token decoded successfully for user: {UserId}, domain: {DomainName}, database: {Database}",
            userId, domainName, databaseName);

        return Ok(response);
    }

    /// <summary>
    /// Get current user's domain information
    /// </summary>
    /// <returns>Domain and database information from JWT token</returns>
    [HttpGet("domain")]
    [Authorize]
    public IActionResult GetDomainInfo()
    {
        var domainName = User.FindFirst("domain_name")?.Value;
        var domainId = User.FindFirst("domain_id")?.Value;
        
        if (string.IsNullOrEmpty(domainName))
        {
            return BadRequest(new 
            { 
                error = "Domain information not found in token",
                message = "JWT token must contain 'domain_name' claim"
            });
        }

        return Ok(new
        {
            domainId = domainId,
            domainName = domainName,
            database = $"mng_{domainName}",
            message = "This is the database that will be used for all operations"
        });
    }

    /// <summary>
    /// Test endpoint to verify roles
    /// </summary>
    /// <returns>User roles and permissions</returns>
    [HttpGet("roles")]
    [Authorize]
    public IActionResult GetRoles()
    {
        var roles = User.FindAll(ClaimTypes.Role)
            .Select(c => c.Value)
            .ToList();

        var isAdmin = User.FindFirst("is_admin")?.Value;

        // Check realm access (KeyCloak style)
        var realmAccessClaim = User.FindFirst("realm_access")?.Value;

        return Ok(new
        {
            roles = roles,
            isAdmin = isAdmin,
            realmAccess = realmAccessClaim,
            hasRoles = roles.Any(),
            message = roles.Any() 
                ? $"User has {roles.Count} role(s)" 
                : "No roles found in token"
        });
    }

    /// <summary>
    /// Health check for authentication system
    /// </summary>
    /// <returns>Authentication system status</returns>
    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult HealthCheck()
    {
        return Ok(new
        {
            status = "healthy",
            authentication = "JWT Bearer",
            authority = "MngKeeper",
            timestamp = DateTime.UtcNow
        });
    }
}

