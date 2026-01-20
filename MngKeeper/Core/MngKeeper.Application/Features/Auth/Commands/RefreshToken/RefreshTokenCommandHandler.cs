using MediatR;
using MngKeeper.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace MngKeeper.Application.Features.Auth.Commands.RefreshToken
{
    public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, RefreshTokenResponse>
    {
        private readonly IKeycloakService _keycloakService;
        private readonly IJwtTokenParserService _jwtTokenParserService;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IDomainRepository _domainRepository;
        private readonly IUserRepository _userRepository;
        private readonly ILicenseService _licenseService;
        private readonly ILogger<RefreshTokenCommandHandler> _logger;

        public RefreshTokenCommandHandler(
            IKeycloakService keycloakService,
            IJwtTokenParserService jwtTokenParserService,
            IJwtTokenService jwtTokenService,
            IDomainRepository domainRepository,
            IUserRepository userRepository,
            ILicenseService licenseService,
            ILogger<RefreshTokenCommandHandler> logger)
        {
            _keycloakService = keycloakService;
            _jwtTokenParserService = jwtTokenParserService;
            _jwtTokenService = jwtTokenService;
            _domainRepository = domainRepository;
            _userRepository = userRepository;
            _licenseService = licenseService;
            _logger = logger;
        }

        public async Task<RefreshTokenResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Refreshing token");

                // Parse refresh token to get realm name
                string realmName;
                if (!string.IsNullOrEmpty(request.DomainName))
                {
                    realmName = request.DomainName.ToLower().Replace(" ", "_");
                }
                else
                {
                    // Try to extract realm from the refresh token (if it's a JWT)
                    // For Keycloak, refresh tokens are usually opaque, so we need domain name
                    return new RefreshTokenResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "DomainName is required for token refresh"
                    };
                }

                // Get domain
                var domain = await _domainRepository.GetByRealmNameAsync(realmName);
                if (domain == null)
                {
                    return new RefreshTokenResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = $"Domain with realm '{realmName}' not found."
                    };
                }

                // Parse the refresh token to get user information first
                var tokenClaims = _jwtTokenParserService.ParseToken(request.RefreshToken);
                
                // Check if user is admin - admins can always refresh tokens even if license expired
                bool isAdmin = false;
                if (tokenClaims != null && !string.IsNullOrEmpty(tokenClaims.Username))
                {
                    var user = await _userRepository.GetByUsernameAsync(tokenClaims.Username, domain.Id);
                    if (user != null && user.Groups != null)
                    {
                        isAdmin = user.Groups.Contains("admins", StringComparer.OrdinalIgnoreCase);
                    }
                    // Also check token claims for is_admin
                    if (!isAdmin)
                    {
                        isAdmin = tokenClaims.IsAdmin;
                    }
                }

                // Check license - block token generation if license expired and blockTokenGeneration is true
                // Exception: Admin users can always refresh tokens to manage licenses
                if (!isAdmin)
                {
                    var isOperationAllowed = await _licenseService.IsOperationAllowedAsync(
                        domain.Name, 
                        MngKeeper.Domain.Entities.LicenseOperation.TokenGeneration);
                    
                    if (!isOperationAllowed)
                    {
                        var validation = await _licenseService.ValidateLicenseAsync(domain.Name);
                        var errorMessage = validation.ExpirationBehavior?.CustomMessage 
                            ?? "Lisans süreniz dolmuştur. Lütfen lisansınızı yenileyin.";
                        
                        _logger.LogWarning("Token refresh blocked due to license expiration for domain: {DomainName}, user: {Username}", 
                            domain.Name, tokenClaims?.Username ?? "unknown");
                        
                        return new RefreshTokenResponse
                        {
                            IsSuccess = false,
                            ErrorMessage = errorMessage
                        };
                    }
                }
                else
                {
                    _logger.LogInformation("Admin user {Username} bypassing license check for token refresh in domain: {DomainName}", 
                        tokenClaims?.Username ?? "unknown", domain.Name);
                }

                // Refresh token from Keycloak
                var keycloakTokenResponse = await _keycloakService.RefreshTokenAsync(domain.RealmName, request.RefreshToken);

                // Parse the new access token to get user information
                var newTokenClaims = _jwtTokenParserService.ParseToken(keycloakTokenResponse.AccessToken);
                
                // Get user from MongoDB to retrieve current groups and profile information
                bool isManager = false;
                List<string>? userGroups = null;
                string? title = null;
                string? department = null;
                int? gender = null;
                string? phoneNumber = null;
                string? photoUrl = null;
                
                if (newTokenClaims != null && !string.IsNullOrEmpty(newTokenClaims.Username))
                {
                    var user = await _userRepository.GetByUsernameAsync(newTokenClaims.Username, domain.Id);
                    if (user != null)
                    {
                        // Get user groups from MongoDB
                        userGroups = user.Groups ?? new List<string>();
                        _logger.LogInformation("User groups retrieved from MongoDB for refresh: {UserGroups}", string.Join(", ", userGroups));
                        
                        // Check if user is admin or manager by checking groups
                        isAdmin = userGroups.Contains("admins");
                        isManager = userGroups.Contains("managers");
                        
                        // Get profile fields
                        title = user.Title;
                        department = user.Department;
                        gender = (int)user.Gender;
                        phoneNumber = user.PhoneNumber;
                        photoUrl = user.PhotoUrl;
                    }
                    else
                    {
                        _logger.LogWarning("User not found in MongoDB for refresh token - Username: {Username}, DomainId: {DomainId}", 
                            newTokenClaims.Username, domain.Id);
                        // Fallback: try to get from token claims or use empty list
                        userGroups = newTokenClaims.Groups ?? new List<string>();
                        if (!isAdmin)
                        {
                            isAdmin = newTokenClaims.IsAdmin;
                        }
                        isManager = newTokenClaims.IsManager;
                    }
                }
                else
                {
                    _logger.LogWarning("Could not parse token claims from refreshed token");
                    userGroups = new List<string>();
                }

                // Add domain claims to the new access token with updated user information
                var enhancedToken = _jwtTokenService.AddDomainClaimToToken(
                    keycloakTokenResponse.AccessToken, 
                    domain.Id, 
                    domain.Name, 
                    isAdmin,
                    isManager,
                    userGroups,
                    title,
                    department,
                    gender,
                    phoneNumber,
                    photoUrl
                );

                var expiresAt = DateTime.UtcNow.AddSeconds(keycloakTokenResponse.ExpiresIn);
                var refreshExpiresAt = DateTime.UtcNow.AddSeconds(keycloakTokenResponse.RefreshExpiresIn);

                _logger.LogInformation("Token refreshed successfully for domain {DomainName}", domain.Name);

                return new RefreshTokenResponse
                {
                    AccessToken = enhancedToken,
                    TokenType = "Bearer",
                    ExpiresIn = keycloakTokenResponse.ExpiresIn,
                    RefreshToken = keycloakTokenResponse.RefreshToken,
                    RefreshExpiresIn = keycloakTokenResponse.RefreshExpiresIn,
                    ExpiresAt = expiresAt,
                    RefreshExpiresAt = refreshExpiresAt,
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                return new RefreshTokenResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Failed to refresh token: {ex.Message}"
                };
            }
        }
    }
}

