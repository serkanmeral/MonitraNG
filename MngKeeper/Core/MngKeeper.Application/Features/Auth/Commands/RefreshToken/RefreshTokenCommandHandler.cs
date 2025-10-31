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
        private readonly ILogger<RefreshTokenCommandHandler> _logger;

        public RefreshTokenCommandHandler(
            IKeycloakService keycloakService,
            IJwtTokenParserService jwtTokenParserService,
            IJwtTokenService jwtTokenService,
            IDomainRepository domainRepository,
            ILogger<RefreshTokenCommandHandler> logger)
        {
            _keycloakService = keycloakService;
            _jwtTokenParserService = jwtTokenParserService;
            _jwtTokenService = jwtTokenService;
            _domainRepository = domainRepository;
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

                // Refresh token from Keycloak
                var keycloakTokenResponse = await _keycloakService.RefreshTokenAsync(domain.RealmName, request.RefreshToken);

                // Add domain claims to the new access token
                var enhancedToken = _jwtTokenService.AddDomainClaimToToken(
                    keycloakTokenResponse.AccessToken, 
                    domain.Id, 
                    domain.Name, 
                    true // Assume admin for now, you can parse from original token if needed
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

