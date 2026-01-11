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
        private readonly IUserRepository _userRepository;
        private readonly ILogger<GetTokenCommandHandler> _logger;

        public GetTokenCommandHandler(
            IDomainRepository domainRepository,
            IKeycloakService keycloakService,
            IJwtTokenService jwtTokenService,
            IUserRepository userRepository,
            ILogger<GetTokenCommandHandler> logger)
        {
            _domainRepository = domainRepository;
            _keycloakService = keycloakService;
            _jwtTokenService = jwtTokenService;
            _userRepository = userRepository;
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

                // Check if user is active in MngKeeper database before authenticating
                var user = await _userRepository.GetByUsernameAsync(actualUsername, domain.Id);
                if (user != null && user.DomainId == domain.Id)
                {
                    if (!user.IsActive)
                    {
                        _logger.LogWarning("Inactive user attempted to get token: {Username} in domain: {Domain}", 
                            actualUsername, domain.Name);
                        
                        return new GetTokenResponse
                        {
                            IsSuccess = false,
                            ErrorMessage = "User account is inactive"
                        };
                    }
                }

                // Get token from Keycloak
                var keycloakTokenResponse = await _keycloakService.GetTokenAsync(domain.RealmName, actualUsername, request.Password);

                // Check if token was obtained successfully
                if (!string.IsNullOrEmpty(keycloakTokenResponse.Error))
                {
                    _logger.LogWarning("Failed to get token from Keycloak for user: {Username} in realm: {RealmName}. Error: {Error}", 
                        actualUsername, domain.RealmName, keycloakTokenResponse.Error);
                    
                    return new GetTokenResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = keycloakTokenResponse.Error switch
                        {
                            "invalid_grant" => "Invalid username or password",
                            "invalid_client" => "Client authentication failed",
                            "unauthorized_client" => "Client not authorized",
                            _ => $"Authentication failed: {keycloakTokenResponse.Error}"
                        }
                    };
                }

                // Check if AccessToken is empty
                if (string.IsNullOrEmpty(keycloakTokenResponse.AccessToken))
                {
                    _logger.LogError("Keycloak returned empty access token for user: {Username} in realm: {RealmName}", 
                        actualUsername, domain.RealmName);
                    
                    return new GetTokenResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = "Failed to obtain access token from authentication server"
                    };
                }

                // Check if user is admin by checking their groups
                var isAdmin = await _keycloakService.IsUserInGroupAsync(domain.RealmName, actualUsername, "admins");
                
                // Check if user is manager by checking their groups
                var isManager = await _keycloakService.IsUserInGroupAsync(domain.RealmName, actualUsername, "managers");

                // Get user from MongoDB (more reliable than Keycloak attributes)
                // Note: We already fetched user above for IsActive check, but we need to fetch again
                // to get profile fields. In the future, we could optimize this.
                _logger.LogInformation("Getting user from MongoDB - Username: {Username}, DomainId: {DomainId}", actualUsername, domain.Id);
                if (user == null)
                {
                    user = await _userRepository.GetByUsernameAsync(actualUsername, domain.Id);
                }
                
                // Extract user profile fields from MongoDB user entity
                string? title = null;
                string? department = null;
                int? gender = null;
                string? phoneNumber = null;
                string? photoUrl = null;
                
                if (user != null)
                {
                    _logger.LogInformation("User found in MongoDB - UserId: {UserId}, Username: {Username}", user.Id, user.Username);
                    title = user.Title;
                    department = user.Department;
                    gender = (int)user.Gender;
                    phoneNumber = user.PhoneNumber;
                    photoUrl = user.PhotoUrl;
                    
                    _logger.LogInformation("User profile fields from MongoDB - Title: {Title}, Department: {Department}, Gender: {Gender}, PhoneNumber: {PhoneNumber}, PhotoUrl: {PhotoUrl}", 
                        title ?? "null", department ?? "null", gender?.ToString() ?? "null", phoneNumber ?? "null", photoUrl ?? "null");
                }
                else
                {
                    _logger.LogWarning("User not found in MongoDB for username: {Username}, DomainId: {DomainId}", actualUsername, domain.Id);
                    
                    // Fallback: Try to get from Keycloak attributes
                    _logger.LogInformation("Trying to get user attributes from Keycloak...");
                    var userAttributes = await _keycloakService.GetUserAttributesAsync(domain.RealmName, actualUsername);
                    if (userAttributes != null && userAttributes.Count > 0)
                    {
                        _logger.LogInformation("User attributes retrieved from Keycloak: {AttributeCount} attributes", userAttributes.Count);
                        userAttributes.TryGetValue("title", out title);
                        userAttributes.TryGetValue("department", out department);
                        if (userAttributes.TryGetValue("gender", out var genderStr) && int.TryParse(genderStr, out var genderValue))
                        {
                            gender = genderValue;
                        }
                        userAttributes.TryGetValue("phoneNumber", out phoneNumber);
                        userAttributes.TryGetValue("photoUrl", out photoUrl);
                    }
                    else
                    {
                        _logger.LogWarning("User attributes not found in Keycloak either");
                    }
                }
                
                // Get user groups from MongoDB (user.Groups field)
                List<string>? userGroups = null;
                if (user != null && user.Groups != null && user.Groups.Count > 0)
                {
                    userGroups = user.Groups;
                    _logger.LogInformation("User groups retrieved from MongoDB: {UserGroups}", string.Join(", ", userGroups));
                }
                else
                {
                    _logger.LogWarning("User groups not found in MongoDB for user: {Username}, DomainId: {DomainId}", actualUsername, domain.Id);
                    userGroups = new List<string>();
                }

                _logger.LogInformation("Final values before adding to token - isManager: {IsManager}, isAdmin: {IsAdmin}, UserGroups: {UserGroups}, Title: {Title}, Department: {Department}, Gender: {Gender}, PhoneNumber: {PhoneNumber}, PhotoUrl: {PhotoUrl}", 
                    isManager, isAdmin, userGroups != null ? string.Join(", ", userGroups) : "null", title ?? "null", department ?? "null", gender?.ToString() ?? "null", phoneNumber ?? "null", photoUrl ?? "null");

                // Add domain claims and user profile fields to the token
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
                    photoUrl);

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
