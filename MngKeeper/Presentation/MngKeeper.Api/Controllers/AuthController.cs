using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngKeeper.Application.Interfaces;
using MngKeeper.Application.Helpers;
using MngKeeper.Api.Attributes;

namespace MngKeeper.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IKeycloakService _keycloakService;
    private readonly IUserRepository _userRepository;
    private readonly IDomainRepository _domainRepository;
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IKeycloakService keycloakService,
        IUserRepository userRepository,
        IDomainRepository domainRepository,
        IPasswordResetTokenRepository passwordResetTokenRepository,
        ILogger<AuthController> logger)
    {
        _keycloakService = keycloakService;
        _userRepository = userRepository;
        _domainRepository = domainRepository;
        _passwordResetTokenRepository = passwordResetTokenRepository;
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
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "invalid_request",
                    ErrorDescription = "Username and password are required"
                });
            }

            // Parse domain from username if format is "domain@username"
            string domainName = request.Domain;
            string username = request.Username;

            if (string.IsNullOrWhiteSpace(domainName))
            {
                // Try to parse domain from username format: "domain@username"
                var parts = username.Split('@', 2);
                if (parts.Length == 2)
                {
                    domainName = parts[0];
                    username = parts[1];
                    _logger.LogInformation("Domain parsed from username format: {Domain}@{Username}", 
                        domainName, username);
                }
                else
                {
                    // If no @ in username, try to use single domain if only one exists
                    var allDomains = await _domainRepository.GetAllAsync();
                    var domainList = allDomains.ToList();
                    
                    if (domainList.Count == 1)
                    {
                        domainName = domainList[0].Name;
                        _logger.LogInformation("Using single domain: {Domain} for username: {Username}", 
                            domainName, username);
                    }
                    else if (domainList.Count == 0)
                    {
                        return BadRequest(new ErrorResponse
                        {
                            Error = "no_domains",
                            ErrorDescription = "No domains found in the system"
                        });
                    }
                    else
                    {
                        return BadRequest(new ErrorResponse
                        {
                            Error = "domain_required",
                            ErrorDescription = $"Multiple domains found ({domainList.Count}). Domain is required. Either provide 'domain' parameter or use 'domain@username' format"
                        });
                    }
                }
            }

            _logger.LogInformation("Token request for user: {Username} in domain: {Domain}", 
                username, domainName);

            // Check if user is active in MngKeeper database before authenticating
            var domain = await _domainRepository.GetByNameAsync(domainName);
            if (domain == null)
            {
                _logger.LogWarning("Domain not found: {Domain}", domainName);
                return Unauthorized(new ErrorResponse
                {
                    Error = "invalid_domain",
                    ErrorDescription = "Domain not found"
                });
            }

            // Get user from database to check IsActive status
            var user = await _userRepository.GetByUsernameAsync(username, domain.Id);
            if (user != null && user.DomainId == domain.Id)
            {
                if (!user.IsActive)
                {
                    _logger.LogWarning("Inactive user attempted to get token: {Username} in domain: {Domain}", 
                        username, domainName);
                    
                    return Unauthorized(new ErrorResponse
                    {
                        Error = "user_inactive",
                        ErrorDescription = "User account is inactive"
                    });
                }
            }

            var tokenResponse = await _keycloakService.GetTokenAsync(
                domainName, 
                username, 
                request.Password);

            if (!string.IsNullOrEmpty(tokenResponse.Error))
            {
                _logger.LogWarning("Token request failed for user: {Username} in domain: {Domain}. Error: {Error}", 
                    username, domainName, tokenResponse.Error);
                
                return Unauthorized(new ErrorResponse
                {
                    Error = tokenResponse.Error,
                    ErrorDescription = "Invalid username or password"
                });
            }

            _logger.LogInformation("Token issued successfully for user: {Username} in domain: {Domain}", 
                username, domainName);

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
            _logger.LogError(ex, "Error processing token request for user: {Username}", 
                request.Username);
            
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

    /// <summary>
    /// Change password for authenticated user
    /// </summary>
    /// <param name="request">Change password request</param>
    /// <returns>Success status</returns>
    [HttpPost("change-password")]
    [AuthenticatedAuthorization]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword) || 
                string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "invalid_request",
                    ErrorDescription = "Current password and new password are required"
                });
            }

            // Get user info from token claims
            var claims = HttpContext.Items["TokenClaims"] as TokenClaims;
            if (claims == null || string.IsNullOrEmpty(claims.DomainName) || string.IsNullOrEmpty(claims.Username))
            {
                return Unauthorized(new ErrorResponse
                {
                    Error = "invalid_token",
                    ErrorDescription = "User information not found in token"
                });
            }

            _logger.LogInformation("Change password request for user: {Username} in domain: {Domain}", 
                claims.Username, claims.DomainName);

            // Validate new password strength
            var passwordValidation = PasswordValidator.ValidatePassword(request.NewPassword);
            if (!passwordValidation.IsValid)
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "invalid_password",
                    ErrorDescription = passwordValidation.ErrorMessage ?? "Password does not meet requirements"
                });
            }

            // Validate current password
            var isCurrentPasswordValid = await _keycloakService.ValidateUserPasswordAsync(
                claims.DomainName, 
                claims.Username, 
                request.CurrentPassword);

            if (!isCurrentPasswordValid)
            {
                return Unauthorized(new ErrorResponse
                {
                    Error = "invalid_password",
                    ErrorDescription = "Current password is incorrect"
                });
            }

            // Get user from database to get Keycloak user ID
            var domain = await _domainRepository.GetByNameAsync(claims.DomainName);
            if (domain == null)
            {
                return Unauthorized(new ErrorResponse
                {
                    Error = "invalid_domain",
                    ErrorDescription = "Domain not found"
                });
            }

            var user = await _userRepository.GetByUsernameAsync(claims.Username, domain.Id);
            if (user == null)
            {
                return Unauthorized(new ErrorResponse
                {
                    Error = "user_not_found",
                    ErrorDescription = "User not found"
                });
            }

            // Update password in Keycloak
            var passwordUpdated = await _keycloakService.UpdateUserPasswordAsync(
                domain.RealmName,
                user.KeycloakUserId,
                request.NewPassword,
                temporary: false);

            if (!passwordUpdated)
            {
                _logger.LogError("Failed to update password in Keycloak for user: {Username} in domain: {Domain}", 
                    claims.Username, claims.DomainName);
                
                return StatusCode(500, new ErrorResponse
                {
                    Error = "password_update_failed",
                    ErrorDescription = "Failed to update password. Please try again later."
                });
            }

            _logger.LogInformation("Password changed successfully for user: {Username} in domain: {Domain}", 
                claims.Username, claims.DomainName);

            return Ok(new { message = "Password changed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing password");
            
            return StatusCode(500, new ErrorResponse
            {
                Error = "server_error",
                ErrorDescription = "An error occurred while processing your request"
            });
        }
    }

    /// <summary>
    /// Reset password using reset token
    /// </summary>
    /// <param name="request">Reset password request</param>
    /// <returns>Success status</returns>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Token) || 
                string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "invalid_request",
                    ErrorDescription = "Token and new password are required"
                });
            }

            _logger.LogInformation("Reset password request with token");

            // Validate new password strength
            var passwordValidation = PasswordValidator.ValidatePassword(request.NewPassword);
            if (!passwordValidation.IsValid)
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "invalid_password",
                    ErrorDescription = passwordValidation.ErrorMessage ?? "Password does not meet requirements"
                });
            }

            // Validate token
            var isValid = await _passwordResetTokenRepository.IsTokenValidAsync(request.Token);
            if (!isValid)
            {
                return Unauthorized(new ErrorResponse
                {
                    Error = "invalid_token",
                    ErrorDescription = "Invalid or expired reset token"
                });
            }

            // Get token entity
            var tokenEntity = await _passwordResetTokenRepository.GetByTokenAsync(request.Token);
            if (tokenEntity == null)
            {
                return Unauthorized(new ErrorResponse
                {
                    Error = "invalid_token",
                    ErrorDescription = "Reset token not found"
                });
            }

            // Get domain
            var domain = await _domainRepository.GetByIdAsync(tokenEntity.DomainId);
            if (domain == null)
            {
                return Unauthorized(new ErrorResponse
                {
                    Error = "invalid_domain",
                    ErrorDescription = "Domain not found"
                });
            }

            // Get user
            var user = await _userRepository.GetByIdAsync(tokenEntity.UserId, tokenEntity.DomainId);
            if (user == null)
            {
                return Unauthorized(new ErrorResponse
                {
                    Error = "user_not_found",
                    ErrorDescription = "User not found"
                });
            }

            // Update password in Keycloak
            var passwordUpdated = await _keycloakService.UpdateUserPasswordAsync(
                domain.RealmName,
                user.KeycloakUserId,
                request.NewPassword,
                temporary: false);

            if (!passwordUpdated)
            {
                _logger.LogError("Failed to update password in Keycloak for user: {UserId} in domain: {Domain}", 
                    user.Id, domain.Name);
                
                return StatusCode(500, new ErrorResponse
                {
                    Error = "password_update_failed",
                    ErrorDescription = "Failed to update password. Please try again later."
                });
            }

            // Mark token as used
            await _passwordResetTokenRepository.MarkTokenAsUsedAsync(request.Token);

            _logger.LogInformation("Password reset successfully for user: {UserId} in domain: {Domain}", 
                user.Id, domain.Name);

            return Ok(new { message = "Password reset successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password");
            
            return StatusCode(500, new ErrorResponse
            {
                Error = "server_error",
                ErrorDescription = "An error occurred while processing your request"
            });
        }
    }

    /// <summary>
    /// Create password reset token for testing (Admin only)
    /// </summary>
    /// <param name="request">Create reset token request</param>
    /// <returns>Reset token</returns>
    [HttpPost("create-reset-token")]
    [AdminAuthorization]
    [ProducesResponseType(typeof(CreateResetTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateResetToken([FromBody] CreateResetTokenRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Domain))
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "invalid_request",
                    ErrorDescription = "Username and domain are required"
                });
            }

            _logger.LogInformation("Creating reset token for user: {Username} in domain: {Domain}", 
                request.Username, request.Domain);

            // Get domain
            var domain = await _domainRepository.GetByNameAsync(request.Domain);
            if (domain == null)
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "invalid_domain",
                    ErrorDescription = "Domain not found"
                });
            }

            // Get user
            var user = await _userRepository.GetByUsernameAsync(request.Username, domain.Id);
            if (user == null)
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "user_not_found",
                    ErrorDescription = "User not found"
                });
            }

            // Generate secure random token
            var tokenBytes = new byte[32];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(tokenBytes);
            }
            // Convert to Base64Url (URL-safe base64)
            var base64Token = Convert.ToBase64String(tokenBytes);
            var token = base64Token.TrimEnd('=').Replace('+', '-').Replace('/', '_');

            // Create token entity
            var resetToken = new MngKeeper.Domain.Entities.PasswordResetToken
            {
                Token = token,
                UserId = user.Id,
                DomainId = domain.Id,
                ExpiresAt = DateTime.UtcNow.AddHours(request.ExpirationHours ?? 1), // Default 1 hour
                IsUsed = false,
                CreatedAt = DateTime.UtcNow,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            };

            // Save token
            await _passwordResetTokenRepository.AddAsync(resetToken);

            _logger.LogInformation("Reset token created successfully for user: {Username} in domain: {Domain}", 
                request.Username, request.Domain);

            return Ok(new CreateResetTokenResponse
            {
                Token = token,
                ExpiresAt = resetToken.ExpiresAt,
                UserId = user.Id,
                Username = user.Username
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating reset token");
            
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
    public string? Domain { get; set; } // Optional: can be parsed from username if format is "domain@username"
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

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class ResetPasswordRequest
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

public class CreateResetTokenRequest
{
    public string Username { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public int? ExpirationHours { get; set; } = 1; // Default 1 hour
}

public class CreateResetTokenResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
}
