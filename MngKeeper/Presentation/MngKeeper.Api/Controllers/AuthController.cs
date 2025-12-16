using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngKeeper.Application.Interfaces;

namespace MngKeeper.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IKeycloakService _keycloakService;
    private readonly IUserRepository _userRepository;
    private readonly IDomainRepository _domainRepository;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IKeycloakService keycloakService,
        IUserRepository userRepository,
        IDomainRepository domainRepository,
        ILogger<AuthController> logger)
    {
        _keycloakService = keycloakService;
        _userRepository = userRepository;
        _domainRepository = domainRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get authentication token for a user
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <returns>JWT token response</returns>
    [HttpPost("token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetToken([FromBody] TokenRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Username) || 
                string.IsNullOrWhiteSpace(request.Password) ||
                string.IsNullOrWhiteSpace(request.Domain))
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "invalid_request",
                    ErrorDescription = "Username, password and domain are required"
                });
            }

            _logger.LogInformation("Token request for user: {Username} in domain: {Domain}", 
                request.Username, request.Domain);

            // Check if user is active in MngKeeper database before authenticating
            var domain = await _domainRepository.GetByNameAsync(request.Domain);
            if (domain == null)
            {
                _logger.LogWarning("Domain not found: {Domain}", request.Domain);
                return Unauthorized(new ErrorResponse
                {
                    Error = "invalid_domain",
                    ErrorDescription = "Domain not found"
                });
            }

            // Get user from database to check IsActive status
            var user = await _userRepository.GetByUsernameAsync(request.Username);
            if (user != null && user.DomainId == domain.Id)
            {
                if (!user.IsActive)
                {
                    _logger.LogWarning("Inactive user attempted to get token: {Username} in domain: {Domain}", 
                        request.Username, request.Domain);
                    
                    return Unauthorized(new ErrorResponse
                    {
                        Error = "user_inactive",
                        ErrorDescription = "User account is inactive"
                    });
                }
            }

            var tokenResponse = await _keycloakService.GetTokenAsync(
                request.Domain, 
                request.Username, 
                request.Password);

            if (!string.IsNullOrEmpty(tokenResponse.Error))
            {
                _logger.LogWarning("Token request failed for user: {Username} in domain: {Domain}. Error: {Error}", 
                    request.Username, request.Domain, tokenResponse.Error);
                
                return Unauthorized(new ErrorResponse
                {
                    Error = tokenResponse.Error,
                    ErrorDescription = "Invalid username or password"
                });
            }

            _logger.LogInformation("Token issued successfully for user: {Username} in domain: {Domain}", 
                request.Username, request.Domain);

            return Ok(new TokenResponse
            {
                AccessToken = tokenResponse.AccessToken,
                RefreshToken = tokenResponse.RefreshToken,
                TokenType = tokenResponse.TokenType,
                ExpiresIn = tokenResponse.ExpiresIn,
                RefreshExpiresIn = tokenResponse.RefreshExpiresIn
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing token request for user: {Username} in domain: {Domain}", 
                request.Username, request.Domain);
            
            return StatusCode(500, new ErrorResponse
            {
                Error = "server_error",
                ErrorDescription = "An error occurred while processing your request"
            });
        }
    }

    /// <summary>
    /// Refresh an expired access token
    /// </summary>
    /// <param name="request">Refresh token request</param>
    /// <returns>New JWT token response</returns>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken) || 
                string.IsNullOrWhiteSpace(request.Domain))
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "invalid_request",
                    ErrorDescription = "Refresh token and domain are required"
                });
            }

            _logger.LogInformation("Refresh token request for domain: {Domain}", request.Domain);

            var tokenResponse = await _keycloakService.RefreshTokenAsync(
                request.Domain, 
                request.RefreshToken);

            if (!string.IsNullOrEmpty(tokenResponse.Error))
            {
                return Unauthorized(new ErrorResponse
                {
                    Error = tokenResponse.Error,
                    ErrorDescription = "Invalid or expired refresh token"
                });
            }

            _logger.LogInformation("Token refreshed successfully for domain: {Domain}", request.Domain);

            return Ok(new TokenResponse
            {
                AccessToken = tokenResponse.AccessToken,
                RefreshToken = tokenResponse.RefreshToken,
                TokenType = tokenResponse.TokenType,
                ExpiresIn = tokenResponse.ExpiresIn,
                RefreshExpiresIn = tokenResponse.RefreshExpiresIn
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing token for domain: {Domain}", request.Domain);
            
            return StatusCode(500, new ErrorResponse
            {
                Error = "server_error",
                ErrorDescription = "An error occurred while processing your request"
            });
        }
    }

    /// <summary>
    /// Revoke a refresh token (logout)
    /// </summary>
    /// <param name="request">Revoke token request</param>
    /// <returns>Success status</returns>
    [HttpPost("revoke")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RevokeToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken) || 
                string.IsNullOrWhiteSpace(request.Domain))
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "invalid_request",
                    ErrorDescription = "Refresh token and domain are required"
                });
            }

            _logger.LogInformation("Revoke token request for domain: {Domain}", request.Domain);

            var success = await _keycloakService.RevokeTokenAsync(
                request.Domain, 
                request.RefreshToken);

            if (!success)
            {
                _logger.LogWarning("Failed to revoke token for domain: {Domain}", request.Domain);
            }

            return Ok(new { message = "Token revoked successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking token for domain: {Domain}", request.Domain);
            
            return StatusCode(500, new ErrorResponse
            {
                Error = "server_error",
                ErrorDescription = "An error occurred while processing your request"
            });
        }
    }
}

// DTOs
public class TokenRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
}

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
}

public class TokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
    public int RefreshExpiresIn { get; set; }
}

public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string ErrorDescription { get; set; } = string.Empty;
}
