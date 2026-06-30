using MediatR;
using MngKeeper.Application.Common;
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
        private readonly ILicenseService _licenseService;
        private readonly ITokenCredentialResolver _tokenCredentialResolver;
        private readonly IPrivilegeGroupResolver _privilegeGroupResolver;
        private readonly IKeycloakToMongoSyncService _directorySyncService;
        private readonly ILogger<GetTokenCommandHandler> _logger;

        public GetTokenCommandHandler(
            IDomainRepository domainRepository,
            IKeycloakService keycloakService,
            IJwtTokenService jwtTokenService,
            IUserRepository userRepository,
            ILicenseService licenseService,
            ITokenCredentialResolver tokenCredentialResolver,
            IPrivilegeGroupResolver privilegeGroupResolver,
            IKeycloakToMongoSyncService directorySyncService,
            ILogger<GetTokenCommandHandler> logger)
        {
            _domainRepository = domainRepository;
            _keycloakService = keycloakService;
            _jwtTokenService = jwtTokenService;
            _userRepository = userRepository;
            _licenseService = licenseService;
            _tokenCredentialResolver = tokenCredentialResolver;
            _privilegeGroupResolver = privilegeGroupResolver;
            _directorySyncService = directorySyncService;
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
                    realmName = request.DomainName.ToLower().Replace(" ", "_");
                    actualUsername = request.Username;
                    _logger.LogInformation("Using provided domain name: {DomainName}, realm: {RealmName}", request.DomainName, realmName);
                }
                else
                {
                    var resolved = await _tokenCredentialResolver.ResolveAsync(request.Username, cancellationToken: cancellationToken);
                    if (!resolved.IsSuccess)
                    {
                        return new GetTokenResponse
                        {
                            IsSuccess = false,
                            ErrorMessage = resolved.ErrorDescription ?? "Could not resolve domain and username"
                        };
                    }

                    realmName = resolved.DomainName.ToLower().Replace(" ", "_");
                    actualUsername = resolved.Username;
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
                    if (!UserLoginEligibility.CanAuthenticate(user))
                    {
                        _logger.LogWarning(
                            "User blocked from token (inactive or out of application scope): {Username} in domain: {Domain}",
                            actualUsername, domain.Name);
                        
                        return new GetTokenResponse
                        {
                            IsSuccess = false,
                            ErrorMessage = user.IsActive
                                ? "User account is not in application scope"
                                : "User account is inactive"
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

                // K4: Keycloak başarılı → tek kullanıcı KC→Mongo sync → güncel gruplar/claim'ler
                var loginSync = await _directorySyncService.SyncUserOnLoginAsync(
                    domain.Id, actualUsername, cancellationToken);
                if (!loginSync.IsSuccess && loginSync.Code != "sync_in_progress" && loginSync.Code != "login_sync_disabled")
                {
                    _logger.LogWarning(
                        "Login directory sync did not complete for {Username}: {Code} — {Message}",
                        actualUsername, loginSync.Code, loginSync.Message);
                }
                else if (loginSync.UsersCreated > 0 || loginSync.UsersUpdated > 0)
                {
                    _logger.LogInformation(
                        "Login directory sync applied for {Username}: created={Created} updated={Updated}",
                        actualUsername, loginSync.UsersCreated, loginSync.UsersUpdated);
                }

                _logger.LogInformation("Getting user from MongoDB - Username: {Username}, DomainId: {DomainId}", actualUsername, domain.Id);
                user = await _userRepository.GetByUsernameAsync(actualUsername, domain.Id);
                if (user == null)
                {
                    var kcUser = await _keycloakService.GetRealmUserByUsernameAsync(
                        domain.RealmName, actualUsername, cancellationToken);
                    if (kcUser != null)
                        user = await _userRepository.GetByUsernameAsync(kcUser.Username, domain.Id);
                }

                if (user != null && !UserLoginEligibility.CanAuthenticate(user))
                {
                    return new GetTokenResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = user.IsActive
                            ? "User account is not in application scope"
                            : "User account is inactive"
                    };
                }

                var isAdmin = await AuthPrivilegeHelper.ResolveIsAdminAsync(
                    _privilegeGroupResolver, _keycloakService, domain, actualUsername, user?.Groups);
                var isManager = await AuthPrivilegeHelper.ResolveIsManagerAsync(
                    _privilegeGroupResolver, _keycloakService, domain, actualUsername, user?.Groups, isAdmin);
                if (user?.Groups != null && user.Groups.Count > 0)
                {
                    isAdmin = _privilegeGroupResolver.IsAdmin(domain, user.Groups);
                    isManager = _privilegeGroupResolver.IsManager(domain, user.Groups);
                }

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

                        _logger.LogWarning(
                            "Token generation blocked due to license expiration for domain: {DomainName}, user: {Username}",
                            domain.Name, actualUsername);

                        return new GetTokenResponse
                        {
                            IsSuccess = false,
                            ErrorMessage = errorMessage
                        };
                    }
                }
                else
                {
                    _logger.LogInformation(
                        "Admin user {Username} bypassing license check for token generation in domain: {DomainName}",
                        actualUsername, domain.Name);
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
                var mngPersonId = user != null && !string.IsNullOrWhiteSpace(user.Id) ? user.Id.Trim() : null;

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
                    photoUrl,
                    mngPersonId);

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

    }
}
