using MediatR;
using MngKeeper.Application.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace MngKeeper.Application.Features.Auth.Commands.GetToken
{
    public class GetTokenCommandHandler : IRequestHandler<GetTokenCommand, GetTokenResponse>
    {
        private readonly IDomainRepository _domainRepository;
        private readonly IKeycloakService _keycloakService;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly ILogger<GetTokenCommandHandler> _logger;

        public GetTokenCommandHandler(
            IDomainRepository domainRepository,
            IKeycloakService keycloakService,
            IJwtTokenService jwtTokenService,
            ILogger<GetTokenCommandHandler> logger)
        {
            _domainRepository = domainRepository;
            _keycloakService = keycloakService;
            _jwtTokenService = jwtTokenService;
            _logger = logger;
        }

        public async Task<GetTokenResponse> Handle(GetTokenCommand request, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Getting token for user: {Username}", request.Username);

                // Determine realm name from domain name or parse from username
                string realmName;
                string actualUsername;
                
                if (!string.IsNullOrEmpty(request.DomainName))
                {
                    // Use provided domain name
                    realmName = request.DomainName.ToLower().Replace(" ", "_");
                    actualUsername = request.Username;
                    _logger.LogInformation("Using provided domain name: {DomainName}, realm: {RealmName}", request.DomainName, realmName);
                }
                else
                {
                    // Parse username to extract realm and actual username
                    (realmName, actualUsername) = ParseUsername(request.Username);
                }
                
                // Get domain by realm name
                var domain = await _domainRepository.GetByRealmNameAsync(realmName);
                if (domain == null)
                {
                    return new GetTokenResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = $"Domain with realm '{realmName}' not found."
                    };
                }

                // Get token from Keycloak
                var keycloakTokenResponse = await _keycloakService.GetTokenAsync(domain.RealmName, actualUsername, request.Password);

                // Check if user is admin by checking their groups
                var isAdmin = await _keycloakService.IsUserInGroupAsync(domain.RealmName, actualUsername, "admins");

                // Add domain claims to the token
                var enhancedToken = _jwtTokenService.AddDomainClaimToToken(keycloakTokenResponse.AccessToken, domain.Id, domain.Name, isAdmin);

                // Parse token to get expiration info (optional)
                var tokenParts = enhancedToken.Split('.');
                if (tokenParts.Length >= 2)
                {
                    try
                    {
                        var payload = tokenParts[1];
                        var padding = 4 - (payload.Length % 4);
                        if (padding != 4)
                        {
                            payload += new string('=', padding);
                        }
                        
                        var jsonBytes = Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/'));
                        var json = System.Text.Encoding.UTF8.GetString(jsonBytes);
                        var tokenData = JsonSerializer.Deserialize<JsonElement>(json);
                        
                        if (tokenData.TryGetProperty("exp", out var expElement))
                        {
                            var exp = expElement.GetInt64();
                            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(exp).DateTime;
                            var expiresIn = (int)(expiresAt - DateTime.UtcNow).TotalSeconds;

                            var refreshExpiresAt = DateTime.UtcNow.AddSeconds(keycloakTokenResponse.RefreshExpiresIn);
                            
                            return new GetTokenResponse
                            {
                                AccessToken = enhancedToken,
                                TokenType = "Bearer",
                                ExpiresIn = expiresIn,
                                RefreshToken = keycloakTokenResponse.RefreshToken,
                                RefreshExpiresIn = keycloakTokenResponse.RefreshExpiresIn,
                                ExpiresAt = expiresAt,
                                RefreshExpiresAt = refreshExpiresAt,
                                IsSuccess = true
                            };
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse token payload");
                    }
                }

                // Fallback response if token parsing fails
                return new GetTokenResponse
                {
                    AccessToken = enhancedToken,
                    TokenType = "Bearer",
                    ExpiresIn = keycloakTokenResponse.ExpiresIn,
                    RefreshToken = keycloakTokenResponse.RefreshToken,
                    RefreshExpiresIn = keycloakTokenResponse.RefreshExpiresIn,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(keycloakTokenResponse.ExpiresIn),
                    RefreshExpiresAt = DateTime.UtcNow.AddSeconds(keycloakTokenResponse.RefreshExpiresIn),
                    IsSuccess = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting token for user: {Username}", request.Username);
                return new GetTokenResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Failed to get token: {ex.Message}"
                };
            }
        }

        private (string realmName, string actualUsername) ParseUsername(string username)
        {
            // Check if username contains @ (multitenant format)
            var parts = username.Split('@');
            
            if (parts.Length == 2)
            {
                // Multitenant format: realm@username
                var realmName = parts[0];
                var actualUsername = parts[1];
                
                _logger.LogInformation("Parsed multitenant username: realm='{RealmName}', username='{ActualUsername}'", realmName, actualUsername);
                return (realmName, actualUsername);
            }
            else
            {
                // Single tenant format: just username (use default realm)
                var defaultRealm = "default"; // TODO: Get from configuration
                var actualUsername = username;
                
                _logger.LogInformation("Parsed single tenant username: realm='{DefaultRealm}', username='{ActualUsername}'", defaultRealm, actualUsername);
                return (defaultRealm, actualUsername);
            }
        }
    }
}
