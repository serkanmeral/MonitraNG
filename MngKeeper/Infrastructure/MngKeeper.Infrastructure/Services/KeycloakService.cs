using MngKeeper.Application.Interfaces;
using MngKeeper.Application.Features.Domain.Commands.CreateDomain;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text;

namespace MngKeeper.Infrastructure.Services
{
    public class KeycloakService : IKeycloakService
    {
        private readonly ILogger<KeycloakService> _logger;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly string _pathPrefix;
        private readonly string _baseUrl;
        private string? _adminToken;

        public KeycloakService(ILogger<KeycloakService> logger, HttpClient httpClient, IConfiguration configuration)
        {
            _logger = logger;
            _httpClient = httpClient;
            _configuration = configuration;

            var baseUrlRaw = configuration["MngKeeperSettings:Keycloak:BaseUrl"] ?? "";
            _baseUrl = baseUrlRaw.TrimEnd('/');

            // Get path prefix from configuration (default: empty string for local, /keycloak for server)
            var pathPrefix = configuration["MngKeeperSettings:Keycloak:PathPrefix"] ?? "";
            if (!string.IsNullOrEmpty(pathPrefix) && !pathPrefix.StartsWith("/"))
                pathPrefix = "/" + pathPrefix;
            if (pathPrefix.EndsWith("/"))
                pathPrefix = pathPrefix.TrimEnd('/');
            _pathPrefix = pathPrefix;

            _logger.LogInformation("KeycloakService initialized with PathPrefix: '{PathPrefix}'", 
                string.IsNullOrEmpty(_pathPrefix) ? "(empty - direct access)" : _pathPrefix);
        }

        /// <summary>
        /// Builds a Keycloak API path for HttpClient. BaseAddress ile birleşince doğru token/realm URL’i oluşmalı.
        /// - BaseUrl origin ise (http://keycloak:8080): "/keycloak/realms/..." gibi absolute path döner.
        /// - BaseUrl path ile bitiyorsa (http://keycloak:8080/keycloak): "realms/..." gibi relative path döner; yoksa Uri birleşimi /keycloak’ı siler ve 404 olur.
        /// </summary>
        private string BuildEndpointPath(string path)
        {
            path = path.TrimStart('/');

            if (string.IsNullOrEmpty(_pathPrefix))
                return "/" + path;

            // BaseUrl zaten path prefix ile bitiyorsa (örn. http://keycloak:8080/keycloak): relative path döndür ki
            // HttpClient BaseAddress + relative = http://keycloak:8080/keycloak/realms/... olsun. "/" + path dönersek
            // Uri birleşimi base’in path’ini silip http://keycloak:8080/realms/... yapar → 404.
            if (!string.IsNullOrEmpty(_baseUrl) && _baseUrl.EndsWith(_pathPrefix, StringComparison.OrdinalIgnoreCase))
                return path;

            return _pathPrefix + "/" + path;
        }

        public async Task<RealmInfo> CreateRealmAsync(string realmName, DomainSettingsDto settings)
        {
            try
            {
                _logger.LogInformation("Creating realm: {RealmName}", realmName);

                await EnsureAdminTokenAsync();

                var realmData = new
                {
                    realm = realmName,
                    enabled = true,
                    displayName = realmName,
                    displayNameHtml = $"<div class=\"kc-logo-text\"><span>{realmName}</span></div>"
                };

                var json = JsonSerializer.Serialize(realmData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

                var response = await _httpClient.PostAsync(BuildEndpointPath("admin/realms"), content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to create realm {RealmName}. Status: {StatusCode}, Error: {Error}", 
                        realmName, response.StatusCode, errorContent);
                    throw new Exception($"Failed to create realm: {response.StatusCode} - {errorContent}");
                }

                // Create custom client scope for domain claims
                await CreateCustomClientScopeAsync(realmName);

                // Note: Protocol mappers (user_groups, isAdmin) should be configured separately
                // using the /api/admin/realms/{realmName}/configure-mappers endpoint
                // This is due to Keycloak permission constraints during realm creation

                // Create default client for the realm (non-critical)
                try
                {
                    await CreateClientAsync(realmName, new CreateClientRequest
                    {
                        ClientId = $"{realmName}-client",
                        Name = $"{realmName} Client",
                        Enabled = true,
                        DirectAccessGrantsEnabled = true,
                        ServiceAccountsEnabled = true
                    });
                    _logger.LogInformation("Client created for realm: {RealmName}", realmName);
                }
                catch (Exception clientEx)
                {
                    _logger.LogWarning(clientEx, "Failed to create client for realm {RealmName} (non-critical)", realmName);
                }

                _logger.LogInformation("Realm created successfully: {RealmName}", realmName);

                return new RealmInfo
                {
                    Name = realmName,
                    Status = "Created",
                    CreatedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating realm: {RealmName}", realmName);
                throw;
            }
        }

        public async Task<UserInfo> CreateUserAsync(string realmName, CreateUserRequest request)
        {
            try
            {
                _logger.LogInformation("Creating user: {Username} in realm {RealmName}", request.Username, realmName);

                await EnsureAdminTokenAsync();

                // Check if user should be admin
                var isAdmin = request.Groups.Contains("admins");

                // Build attributes dictionary
                var attributes = new Dictionary<string, string[]>
                {
                    ["domain"] = new[] { realmName },
                    ["isAdmin"] = new[] { isAdmin.ToString().ToLower() }
                };

                // Add optional attributes if provided
                if (!string.IsNullOrEmpty(request.Title))
                {
                    attributes["title"] = new[] { request.Title };
                }
                if (!string.IsNullOrEmpty(request.Department))
                {
                    attributes["department"] = new[] { request.Department };
                }
                if (request.Gender >= 0 && request.Gender <= 2)
                {
                    attributes["gender"] = new[] { request.Gender.ToString() };
                }
                if (!string.IsNullOrEmpty(request.PhoneNumber))
                {
                    attributes["phoneNumber"] = new[] { request.PhoneNumber };
                }
                if (!string.IsNullOrEmpty(request.PhotoUrl))
                {
                    attributes["photoUrl"] = new[] { request.PhotoUrl };
                }

                // Build user data object - only include credentials if password is provided
                var userDataObject = new Dictionary<string, object>
                {
                    ["username"] = request.Username,
                    ["email"] = request.Email,
                    ["firstName"] = request.FirstName,
                    ["lastName"] = request.LastName,
                    ["enabled"] = true,
                    ["emailVerified"] = true,
                    ["attributes"] = attributes
                };

                // Only include credentials if password is provided
                // If no password, user can set it later via reset password
                if (!string.IsNullOrEmpty(request.Password))
                {
                    userDataObject["credentials"] = new[]
                    {
                        new
                        {
                            type = "password",
                            value = request.Password,
                            temporary = false
                        }
                    };
                }

                var userData = userDataObject;

                var json = JsonSerializer.Serialize(userData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

                var response = await _httpClient.PostAsync(BuildEndpointPath($"admin/realms/{realmName}/users"), content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to create user {Username} in realm {RealmName}. Status: {StatusCode}, Error: {Error}", 
                        request.Username, realmName, response.StatusCode, errorContent);
                    throw new Exception($"Failed to create user: {response.StatusCode} - {errorContent}");
                }

                // Get the created user ID from the Location header
                var locationHeader = response.Headers.Location?.ToString();
                var userId = locationHeader?.Split('/').Last() ?? Guid.NewGuid().ToString();

                _logger.LogInformation("User created successfully: {Username} in realm {RealmName}", request.Username, realmName);

                return new UserInfo
                {
                    Id = userId,
                    Username = request.Username,
                    Email = request.Email,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user: {Username} in realm {RealmName}", request.Username, realmName);
                throw;
            }
        }

        public async Task<ClientInfo> CreateClientAsync(string realmName, CreateClientRequest request)
        {
            try
            {
                _logger.LogInformation("Creating client: {ClientId} in realm {RealmName}", request.ClientId, realmName);

                await EnsureAdminTokenAsync();

                var clientData = new
                {
                    clientId = request.ClientId,
                    name = request.Name,
                    enabled = request.Enabled,
                    clientAuthenticatorType = "client-secret",
                    redirectUris = new[] { "*" },
                    webOrigins = new[] { "*" },
                    publicClient = false,
                    directAccessGrantsEnabled = request.DirectAccessGrantsEnabled,
                    serviceAccountsEnabled = request.ServiceAccountsEnabled,
                    standardFlowEnabled = true,
                    implicitFlowEnabled = false,
                    protocol = "openid-connect"
                };

                var json = JsonSerializer.Serialize(clientData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

                var response = await _httpClient.PostAsync(BuildEndpointPath($"admin/realms/{realmName}/clients"), content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to create client {ClientId} in realm {RealmName}. Status: {StatusCode}, Error: {Error}", 
                        request.ClientId, realmName, response.StatusCode, errorContent);
                    throw new Exception($"Failed to create client: {response.StatusCode} - {errorContent}");
                }

                // Get the created client ID from the Location header
                var locationHeader = response.Headers.Location?.ToString();
                var clientUuid = locationHeader?.Split('/').Last() ?? Guid.NewGuid().ToString();

                // Get client secret
                var secretResponse = await _httpClient.GetAsync(BuildEndpointPath($"admin/realms/{realmName}/clients/{clientUuid}/client-secret"));
                string clientSecret = string.Empty;
                
                if (secretResponse.IsSuccessStatusCode)
                {
                    var secretJson = await secretResponse.Content.ReadAsStringAsync();
                    var secretData = JsonSerializer.Deserialize<JsonElement>(secretJson);
                    clientSecret = secretData.GetProperty("value").GetString() ?? string.Empty;
                }

                _logger.LogInformation("Client created successfully: {ClientId} in realm {RealmName}", request.ClientId, realmName);

                return new ClientInfo
                {
                    Id = clientUuid,
                    ClientId = request.ClientId,
                    ClientSecret = clientSecret,
                    Enabled = request.Enabled,
                    CreatedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating client: {ClientId} in realm {RealmName}", request.ClientId, realmName);
                throw;
            }
        }

        public async Task<GroupInfo> CreateGroupAsync(string realmName, CreateGroupRequest request)
        {
            try
            {
                _logger.LogInformation("Creating group: {GroupName} in realm {RealmName}", request.Name, realmName);

                await EnsureAdminTokenAsync();

                var groupData = new
                {
                    name = request.Name
                };

                var json = JsonSerializer.Serialize(groupData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

                var response = await _httpClient.PostAsync(BuildEndpointPath($"admin/realms/{realmName}/groups"), content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to create group {GroupName} in realm {RealmName}. Status: {StatusCode}, Error: {Error}", 
                        request.Name, realmName, response.StatusCode, errorContent);
                    throw new Exception($"Failed to create group: {response.StatusCode} - {errorContent}");
                }

                // Get the created group ID from the Location header
                var locationHeader = response.Headers.Location?.ToString();
                var groupId = locationHeader?.Split('/').Last() ?? Guid.NewGuid().ToString();

                _logger.LogInformation("Group created successfully: {GroupName} in realm {RealmName}", request.Name, realmName);

                return new GroupInfo
                {
                    Id = groupId,
                    Name = request.Name,
                    Description = request.Description,
                    CreatedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating group: {GroupName} in realm {RealmName}", request.Name, realmName);
                throw;
            }
        }

        public async Task<bool> AddUserToGroupAsync(string realmName, string userId, string groupName)
        {
            try
            {
                _logger.LogInformation("Adding user {UserId} to group {GroupName} in realm {RealmName}", userId, groupName, realmName);

                await EnsureAdminTokenAsync();

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

                // First, get the group ID by name
                var groupsResponse = await _httpClient.GetAsync(BuildEndpointPath($"admin/realms/{realmName}/groups"));
                if (!groupsResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get groups for realm {RealmName}", realmName);
                    return false;
                }

                var groupsJson = await groupsResponse.Content.ReadAsStringAsync();
                var groups = JsonSerializer.Deserialize<JsonElement[]>(groupsJson);
                
                string? groupId = null;
                foreach (var group in groups)
                {
                    if (group.GetProperty("name").GetString() == groupName)
                    {
                        groupId = group.GetProperty("id").GetString();
                        break;
                    }
                }

                if (groupId == null)
                {
                    _logger.LogError("Group {GroupName} not found in realm {RealmName}", groupName, realmName);
                    return false;
                }

                // Check if user is already in the group
                var userGroupsResponse = await _httpClient.GetAsync(BuildEndpointPath($"admin/realms/{realmName}/users/{userId}/groups"));
                if (userGroupsResponse.IsSuccessStatusCode)
                {
                    var userGroupsJson = await userGroupsResponse.Content.ReadAsStringAsync();
                    var userGroups = JsonSerializer.Deserialize<JsonElement[]>(userGroupsJson);
                    
                    if (userGroups != null)
                    {
                        foreach (var group in userGroups)
                        {
                            if (group.GetProperty("id").GetString() == groupId)
                            {
                                _logger.LogInformation("User {UserId} is already in group {GroupName} in realm {RealmName}", userId, groupName, realmName);
                                return true; // User is already in the group, consider it success
                            }
                        }
                    }
                }

                // Add user to group
                var response = await _httpClient.PutAsync(BuildEndpointPath($"admin/realms/{realmName}/users/{userId}/groups/{groupId}"), null);
                
                if (!response.IsSuccessStatusCode)
                {
                    // Check if it's a conflict (user already in group) - this can happen in race conditions
                    if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                    {
                        _logger.LogInformation("User {UserId} is already in group {GroupName} in realm {RealmName} (409 Conflict)", userId, groupName, realmName);
                        return true; // User is already in the group, consider it success
                    }
                    
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to add user {UserId} to group {GroupName} in realm {RealmName}. Status: {StatusCode}, Error: {Error}", 
                        userId, groupName, realmName, response.StatusCode, errorContent);
                    return false;
                }

                _logger.LogInformation("User {UserId} added to group {GroupName} in realm {RealmName}", userId, groupName, realmName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding user {UserId} to group {GroupName} in realm {RealmName}", userId, groupName, realmName);
                return false;
            }
        }

        public async Task<bool> RemoveUserFromGroupAsync(string realmName, string userId, string groupName)
        {
            try
            {
                _logger.LogInformation("Removing user {UserId} from group {GroupName} in realm {RealmName}", userId, groupName, realmName);

                await EnsureAdminTokenAsync();

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

                // First, get the group ID by name
                var groupsResponse = await _httpClient.GetAsync(BuildEndpointPath($"admin/realms/{realmName}/groups"));
                if (!groupsResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get groups for realm {RealmName}", realmName);
                    return false;
                }

                var groupsJson = await groupsResponse.Content.ReadAsStringAsync();
                var groups = JsonSerializer.Deserialize<JsonElement[]>(groupsJson);
                
                string? groupId = null;
                foreach (var group in groups ?? Array.Empty<JsonElement>())
                {
                    if (group.GetProperty("name").GetString() == groupName)
                    {
                        groupId = group.GetProperty("id").GetString();
                        break;
                    }
                }

                if (string.IsNullOrEmpty(groupId))
                {
                    _logger.LogWarning("Group {GroupName} not found in realm {RealmName}", groupName, realmName);
                    return false;
                }

                // Remove user from group
                var response = await _httpClient.DeleteAsync(BuildEndpointPath($"admin/realms/{realmName}/users/{userId}/groups/{groupId}"));
                
                if (!response.IsSuccessStatusCode)
                {
                    // 404 means user is not in the group, which is acceptable (idempotent operation)
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        _logger.LogInformation("User {UserId} is not in group {GroupName} in realm {RealmName} (404 Not Found) - considering success", userId, groupName, realmName);
                        return true;
                    }
                    
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to remove user {UserId} from group {GroupName} in realm {RealmName}. Status: {StatusCode}, Error: {Error}", 
                        userId, groupName, realmName, response.StatusCode, errorContent);
                    return false;
                }

                _logger.LogInformation("User {UserId} removed from group {GroupName} in realm {RealmName}", userId, groupName, realmName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing user {UserId} from group {GroupName} in realm {RealmName}", userId, groupName, realmName);
                return false;
            }
        }

        public async Task<bool> IsUserInGroupAsync(string realmName, string username, string groupName)
        {
            try
            {
                _logger.LogInformation("Checking if user {Username} is in group {GroupName} in realm {RealmName}", username, groupName, realmName);

                await EnsureAdminTokenAsync();

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

                // First, get the user by username
                var usersResponse = await _httpClient.GetAsync(BuildEndpointPath($"admin/realms/{realmName}/users?username={username}"));
                if (!usersResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get user {Username} for realm {RealmName}", username, realmName);
                    return false;
                }

                var usersJson = await usersResponse.Content.ReadAsStringAsync();
                var users = JsonSerializer.Deserialize<JsonElement[]>(usersJson);
                
                if (users == null || users.Length == 0)
                {
                    _logger.LogError("User {Username} not found in realm {RealmName}", username, realmName);
                    return false;
                }

                var userId = users[0].GetProperty("id").GetString();
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogError("User ID not found for user {Username} in realm {RealmName}", username, realmName);
                    return false;
                }

                // Get user's groups
                var userGroupsResponse = await _httpClient.GetAsync(BuildEndpointPath($"admin/realms/{realmName}/users/{userId}/groups"));
                if (!userGroupsResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get groups for user {Username} in realm {RealmName}", username, realmName);
                    return false;
                }

                var userGroupsJson = await userGroupsResponse.Content.ReadAsStringAsync();
                var userGroups = JsonSerializer.Deserialize<JsonElement[]>(userGroupsJson);
                
                if (userGroups == null)
                {
                    _logger.LogInformation("User {Username} has no groups in realm {RealmName}", username, realmName);
                    return false;
                }

                // Check if user is in the specified group
                foreach (var group in userGroups)
                {
                    var groupNameFromKeycloak = group.GetProperty("name").GetString();
                    if (groupNameFromKeycloak == groupName)
                    {
                        _logger.LogInformation("User {Username} is in group {GroupName} in realm {RealmName}", username, groupName, realmName);
                        return true;
                    }
                }

                _logger.LogInformation("User {Username} is not in group {GroupName} in realm {RealmName}", username, groupName, realmName);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if user {Username} is in group {GroupName} in realm {RealmName}", username, groupName, realmName);
                return false;
            }
        }

        public async Task<Dictionary<string, string>?> GetUserAttributesAsync(string realmName, string username)
        {
            try
            {
                _logger.LogInformation("Getting user attributes for: {Username} in realm {RealmName}", username, realmName);

                await EnsureAdminTokenAsync();

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

                // First, get the user by username
                var usersResponse = await _httpClient.GetAsync(BuildEndpointPath($"admin/realms/{realmName}/users?username={username}"));
                if (!usersResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get user {Username} for realm {RealmName}", username, realmName);
                    return null;
                }

                var usersJson = await usersResponse.Content.ReadAsStringAsync();
                var users = JsonSerializer.Deserialize<JsonElement[]>(usersJson);
                
                if (users == null || users.Length == 0)
                {
                    _logger.LogError("User {Username} not found in realm {RealmName}", username, realmName);
                    return null;
                }

                var user = users[0];
                
                // Extract attributes
                var attributes = new Dictionary<string, string>();
                
                if (user.TryGetProperty("attributes", out var attributesElement))
                {
                    foreach (var attr in attributesElement.EnumerateObject())
                    {
                        // Keycloak attributes are arrays, take first element
                        if (attr.Value.ValueKind == JsonValueKind.Array && attr.Value.GetArrayLength() > 0)
                        {
                            attributes[attr.Name] = attr.Value[0].GetString() ?? string.Empty;
                        }
                        else if (attr.Value.ValueKind == JsonValueKind.String)
                        {
                            attributes[attr.Name] = attr.Value.GetString() ?? string.Empty;
                        }
                    }
                }

                _logger.LogInformation("User attributes retrieved for {Username}: {AttributeCount} attributes", username, attributes.Count);
                return attributes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user attributes for {Username} in realm {RealmName}", username, realmName);
                return null;
            }
        }

        public async Task<KeycloakTokenResponse> GetTokenAsync(string realmName, string username, string password)
        {
            try
            {
                _logger.LogInformation("Getting token for user: {Username} in realm {RealmName}", username, realmName);

                // Use admin-cli client (available in all realms by default)
                var formContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("username", username),
                    new KeyValuePair<string, string>("password", password),
                    new KeyValuePair<string, string>("grant_type", "password"),
                    new KeyValuePair<string, string>("client_id", "admin-cli"),
                    new KeyValuePair<string, string>("scope", "profile email offline_access") // offline_access for refresh token
                });

                var response = await _httpClient.PostAsync(BuildEndpointPath($"realms/{realmName}/protocol/openid-connect/token"), formContent);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to get token for user {Username} in realm {RealmName}. Status: {StatusCode}, Error: {Error}", 
                        username, realmName, response.StatusCode, errorContent);
                    
                    // Parse error response
                    try
                    {
                        var errorJson = JsonSerializer.Deserialize<JsonElement>(errorContent);
                        var error = errorJson.GetProperty("error").GetString();
                        
                        return new KeycloakTokenResponse
                        {
                            Error = error ?? "unknown_error"
                        };
                    }
                    catch
                    {
                        return new KeycloakTokenResponse
                        {
                            Error = "authentication_failed"
                        };
                    }
                }

                var tokenResponseJson = await response.Content.ReadAsStringAsync();
                var tokenJson = JsonSerializer.Deserialize<JsonElement>(tokenResponseJson);
                
                var tokenResponse = new KeycloakTokenResponse
                {
                    AccessToken = tokenJson.GetProperty("access_token").GetString() ?? string.Empty,
                    RefreshToken = tokenJson.TryGetProperty("refresh_token", out var refreshToken) ? refreshToken.GetString() ?? string.Empty : string.Empty,
                    ExpiresIn = tokenJson.GetProperty("expires_in").GetInt32(),
                    RefreshExpiresIn = tokenJson.TryGetProperty("refresh_expires_in", out var refreshExpiresIn) ? refreshExpiresIn.GetInt32() : 0,
                    TokenType = tokenJson.GetProperty("token_type").GetString() ?? "Bearer"
                };

                _logger.LogInformation("Token obtained successfully for user: {Username} in realm {RealmName}", username, realmName);

                return tokenResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting token for user: {Username} in realm {RealmName}", username, realmName);
                throw;
            }
        }

        public async Task<KeycloakTokenResponse> RefreshTokenAsync(string realmName, string refreshToken)
        {
            try
            {
                _logger.LogInformation("Refreshing token for realm {RealmName}", realmName);

                // Use admin-cli client (available in all realms by default)
                var formContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "refresh_token"),
                    new KeyValuePair<string, string>("refresh_token", refreshToken),
                    new KeyValuePair<string, string>("client_id", "admin-cli")
                });

                var response = await _httpClient.PostAsync(BuildEndpointPath($"realms/{realmName}/protocol/openid-connect/token"), formContent);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to refresh token in realm {RealmName}. Status: {StatusCode}, Error: {Error}", 
                        realmName, response.StatusCode, errorContent);
                    throw new Exception($"Failed to refresh token: {response.StatusCode} - {errorContent}");
                }

                var tokenResponseJson = await response.Content.ReadAsStringAsync();
                var tokenJson = JsonSerializer.Deserialize<JsonElement>(tokenResponseJson);
                
                var tokenResponse = new KeycloakTokenResponse
                {
                    AccessToken = tokenJson.GetProperty("access_token").GetString() ?? string.Empty,
                    RefreshToken = tokenJson.TryGetProperty("refresh_token", out var newRefreshToken) ? newRefreshToken.GetString() ?? string.Empty : string.Empty,
                    ExpiresIn = tokenJson.GetProperty("expires_in").GetInt32(),
                    RefreshExpiresIn = tokenJson.TryGetProperty("refresh_expires_in", out var refreshExpiresIn) ? refreshExpiresIn.GetInt32() : 0,
                    TokenType = tokenJson.GetProperty("token_type").GetString() ?? "Bearer"
                };

                _logger.LogInformation("Token refreshed successfully for realm {RealmName}", realmName);

                return tokenResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token for realm {RealmName}", realmName);
                throw;
            }
        }

        public async Task<bool> RevokeTokenAsync(string realmName, string refreshToken)
        {
            try
            {
                _logger.LogInformation("Revoking token for realm {RealmName}", realmName);

                // Use admin-cli client (available in all realms by default)
                var formContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("token", refreshToken),
                    new KeyValuePair<string, string>("client_id", "admin-cli"),
                    new KeyValuePair<string, string>("token_type_hint", "refresh_token")
                });

                var response = await _httpClient.PostAsync(BuildEndpointPath($"realms/{realmName}/protocol/openid-connect/revoke"), formContent);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to revoke token in realm {RealmName}. Status: {StatusCode}, Error: {Error}", 
                        realmName, response.StatusCode, errorContent);
                    return false;
                }

                _logger.LogInformation("Token revoked successfully for realm {RealmName}", realmName);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking token for realm {RealmName}", realmName);
                return false;
            }
        }

        public async Task<bool> ValidateTokenAsync(string token)
        {
            try
            {
                _logger.LogInformation("Validating token");

                // TODO: Implement actual Keycloak integration
                await Task.Delay(100); // Simulate async operation

                return token.StartsWith("mock-jwt-token-");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token");
                return false;
            }
        }

        public async Task<bool> DeleteRealmAsync(string realmName)
        {
            try
            {
                _logger.LogInformation("Deleting realm: {RealmName}", realmName);

                // TODO: Implement actual Keycloak integration
                await Task.Delay(100); // Simulate async operation

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting realm: {RealmName}", realmName);
                return false;
            }
        }

        public async Task<bool> DeleteUserAsync(string realmName, string userId)
        {
            try
            {
                _logger.LogInformation("Deleting user: {UserId} in realm {RealmName}", userId, realmName);

                // TODO: Implement actual Keycloak integration
                await Task.Delay(100); // Simulate async operation

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user: {UserId} in realm {RealmName}", userId, realmName);
                return false;
            }
        }

        public async Task<bool> DeleteGroupAsync(string realmName, string groupName)
        {
            try
            {
                _logger.LogInformation("Deleting group: {GroupName} in realm {RealmName}", groupName, realmName);

                await EnsureAdminTokenAsync();

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

                // First, get the group ID by name
                var groupsResponse = await _httpClient.GetAsync(BuildEndpointPath($"admin/realms/{realmName}/groups"));
                if (!groupsResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get groups for realm {RealmName}", realmName);
                    return false;
                }

                var groupsJson = await groupsResponse.Content.ReadAsStringAsync();
                var groups = JsonSerializer.Deserialize<JsonElement[]>(groupsJson);
                
                string? groupId = null;
                foreach (var group in groups ?? Array.Empty<JsonElement>())
                {
                    if (group.GetProperty("name").GetString() == groupName)
                    {
                        groupId = group.GetProperty("id").GetString();
                        break;
                    }
                }

                if (string.IsNullOrEmpty(groupId))
                {
                    _logger.LogWarning("Group {GroupName} not found in realm {RealmName} - may have been already deleted", groupName, realmName);
                    return true; // Group doesn't exist, consider it success
                }

                // Delete the group from Keycloak
                var deleteResponse = await _httpClient.DeleteAsync(BuildEndpointPath($"admin/realms/{realmName}/groups/{groupId}"));
                if (!deleteResponse.IsSuccessStatusCode)
                {
                    var errorContent = await deleteResponse.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to delete group {GroupName} (ID: {GroupId}) in realm {RealmName}. Status: {StatusCode}, Error: {Error}", 
                        groupName, groupId, realmName, deleteResponse.StatusCode, errorContent);
                    return false;
                }

                _logger.LogInformation("Group deleted successfully: {GroupName} (ID: {GroupId}) in realm {RealmName}", groupName, groupId, realmName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting group: {GroupName} in realm {RealmName}", groupName, realmName);
                return false;
            }
        }

        public async Task<bool> UpdateGroupAsync(string realmName, string oldGroupName, string newGroupName, string? description = null)
        {
            try
            {
                _logger.LogInformation("Updating group: {OldGroupName} to {NewGroupName} in realm {RealmName}", oldGroupName, newGroupName, realmName);

                await EnsureAdminTokenAsync();

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

                // First, get the group ID by old name
                var groupsResponse = await _httpClient.GetAsync(BuildEndpointPath($"admin/realms/{realmName}/groups"));
                if (!groupsResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get groups for realm {RealmName}", realmName);
                    return false;
                }

                var groupsJson = await groupsResponse.Content.ReadAsStringAsync();
                var groups = JsonSerializer.Deserialize<JsonElement[]>(groupsJson);
                
                string? groupId = null;
                foreach (var group in groups ?? Array.Empty<JsonElement>())
                {
                    if (group.GetProperty("name").GetString() == oldGroupName)
                    {
                        groupId = group.GetProperty("id").GetString();
                        break;
                    }
                }

                if (string.IsNullOrEmpty(groupId))
                {
                    _logger.LogWarning("Group {OldGroupName} not found in realm {RealmName}", oldGroupName, realmName);
                    return false;
                }

                // Prepare update data - Keycloak only allows updating name
                var updateData = new Dictionary<string, object>
                {
                    ["name"] = newGroupName
                };

                var json = JsonSerializer.Serialize(updateData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Update the group in Keycloak using PUT request
                var updateResponse = await _httpClient.PutAsync(BuildEndpointPath($"admin/realms/{realmName}/groups/{groupId}"), content);
                if (!updateResponse.IsSuccessStatusCode)
                {
                    var errorContent = await updateResponse.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to update group {OldGroupName} (ID: {GroupId}) in realm {RealmName}. Status: {StatusCode}, Error: {Error}", 
                        oldGroupName, groupId, realmName, updateResponse.StatusCode, errorContent);
                    return false;
                }

                _logger.LogInformation("Group updated successfully: {OldGroupName} to {NewGroupName} (ID: {GroupId}) in realm {RealmName}", 
                    oldGroupName, newGroupName, groupId, realmName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating group {OldGroupName} to {NewGroupName} in realm {RealmName}", oldGroupName, newGroupName, realmName);
                return false;
            }
        }

        private async Task EnsureAdminTokenAsync()
        {
            if (!string.IsNullOrEmpty(_adminToken))
                return;

            try
            {
                var adminUsername = _configuration["MngKeeperSettings:Keycloak:AdminUsername"];
                var adminPassword = _configuration["MngKeeperSettings:Keycloak:AdminPassword"];
                var clientId = _configuration["MngKeeperSettings:Keycloak:ClientId"];
                var clientSecret = _configuration["MngKeeperSettings:Keycloak:ClientSecret"];

                var formData = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("username", adminUsername!),
                    new KeyValuePair<string, string>("password", adminPassword!),
                    new KeyValuePair<string, string>("grant_type", "password"),
                    new KeyValuePair<string, string>("client_id", clientId!)
                };
                
                // Only add client_secret if it's provided (for confidential clients)
                if (!string.IsNullOrEmpty(clientSecret))
                {
                    formData.Add(new KeyValuePair<string, string>("client_secret", clientSecret!));
                }
                
                var formContent = new FormUrlEncodedContent(formData);

                var tokenPath = BuildEndpointPath("realms/master/protocol/openid-connect/token");
                var effectiveUrl = _httpClient.BaseAddress != null
                    ? new Uri(_httpClient.BaseAddress, tokenPath).ToString()
                    : (string.IsNullOrEmpty(_baseUrl) ? tokenPath : new Uri(new Uri(_baseUrl), tokenPath).ToString());
                _logger.LogInformation(
                    "Keycloak admin token request: BaseAddress={BaseAddress}, RequestPath={RequestPath}, EffectiveUrl={EffectiveUrl}",
                    _httpClient.BaseAddress?.ToString(), tokenPath, effectiveUrl);

                var response = await _httpClient.PostAsync(tokenPath, formContent);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to get admin token. Status: {StatusCode}, Error: {Error}", 
                        response.StatusCode, errorContent);
                    throw new Exception($"Failed to get admin token: {response.StatusCode} - {errorContent}");
                }

                var tokenResponse = await response.Content.ReadAsStringAsync();
                var tokenJson = JsonSerializer.Deserialize<JsonElement>(tokenResponse);
                _adminToken = tokenJson.GetProperty("access_token").GetString();

                _logger.LogInformation("Admin token obtained successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting admin token");
                throw;
            }
        }

        private async Task CreateCustomClientScopeAsync(string realmName)
        {
            try
            {
                _logger.LogInformation("Creating custom client scope for realm: {RealmName}", realmName);

                await EnsureAdminTokenAsync();

                // Create client scope
                var clientScopeData = new
                {
                    name = "custom-domain",
                    description = "Custom domain claims for MngKeeper",
                    protocol = "openid-connect",
                    attributes = new Dictionary<string, string>
                    {
                        ["include.in.token"] = "true",
                        ["include.in.id.token"] = "true",
                        ["include.in.access.token"] = "true",
                        ["include.in.userinfo"] = "true"
                    }
                };

                var json = JsonSerializer.Serialize(clientScopeData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

                var response = await _httpClient.PostAsync(BuildEndpointPath($"admin/realms/{realmName}/client-scopes"), content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Failed to create client scope for realm {RealmName}. Status: {StatusCode}, Error: {Error}", 
                        realmName, response.StatusCode, errorContent);
                    return; // Don't throw, just log warning
                }

                var locationHeader = response.Headers.Location?.ToString();
                var scopeId = locationHeader?.Split('/').Last();

                if (!string.IsNullOrEmpty(scopeId))
                {
                    // Add custom protocol mapper for domain claim
                    await AddDomainProtocolMapperAsync(realmName, scopeId);
                }

                _logger.LogInformation("Custom client scope created successfully for realm: {RealmName}", realmName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error creating custom client scope for realm: {RealmName}", realmName);
                // Don't throw, just log warning
            }
        }

        private async Task AddDomainProtocolMapperAsync(string realmName, string scopeId)
        {
            try
            {
                var mapperData = new
                {
                    name = "domain-claim",
                    protocol = "openid-connect",
                    protocolMapper = "oidc-usermodel-attribute-mapper",
                    config = new Dictionary<string, string>
                    {
                        ["userinfo.token.claim"] = "true",
                        ["user.attribute"] = "domain",
                        ["id.token.claim"] = "true",
                        ["access.token.claim"] = "true",
                        ["claim.name"] = "domain",
                        ["jsonType.label"] = "String"
                    }
                };

                var json = JsonSerializer.Serialize(mapperData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

                var response = await _httpClient.PostAsync(BuildEndpointPath($"admin/realms/{realmName}/client-scopes/{scopeId}/protocol-mappers/models"), content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("Failed to add domain protocol mapper for realm {RealmName}. Status: {StatusCode}, Error: {Error}", 
                        realmName, response.StatusCode, errorContent);
                }
                else
                {
                    _logger.LogInformation("Domain protocol mapper added successfully for realm: {RealmName}", realmName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error adding domain protocol mapper for realm: {RealmName}", realmName);
            }
        }

        public async Task<bool> UpdateUserPasswordAsync(string realmName, string userId, string newPassword, bool temporary = false)
        {
            try
            {
                _logger.LogInformation("Updating password for user {UserId} in realm {RealmName}", userId, realmName);

                await EnsureAdminTokenAsync();

                var passwordData = new
                {
                    type = "password",
                    value = newPassword,
                    temporary = temporary
                };

                var json = JsonSerializer.Serialize(passwordData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

                var response = await _httpClient.PutAsync(BuildEndpointPath($"admin/realms/{realmName}/users/{userId}/reset-password"), content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to update password for user {UserId} in realm {RealmName}. Status: {StatusCode}, Error: {Error}", 
                        userId, realmName, response.StatusCode, errorContent);
                    return false;
                }

                _logger.LogInformation("Password updated successfully for user {UserId} in realm {RealmName}", userId, realmName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating password for user {UserId} in realm {RealmName}", userId, realmName);
                return false;
            }
        }

        public async Task<bool> UpdateUserAsync(string realmName, string userId, UpdateUserRequest request)
        {
            try
            {
                _logger.LogInformation("Updating user {UserId} in realm {RealmName}", userId, realmName);

                await EnsureAdminTokenAsync();

                // Build user update data
                var userData = new Dictionary<string, object>();

                // Basic user properties
                if (!string.IsNullOrEmpty(request.Username))
                    userData["username"] = request.Username;
                
                if (!string.IsNullOrEmpty(request.Email))
                    userData["email"] = request.Email;
                
                if (!string.IsNullOrEmpty(request.FirstName))
                    userData["firstName"] = request.FirstName;
                
                if (!string.IsNullOrEmpty(request.LastName))
                    userData["lastName"] = request.LastName;

                // Attributes (custom fields)
                var attributes = new Dictionary<string, object>();
                
                if (!string.IsNullOrEmpty(request.Title))
                    attributes["title"] = new[] { request.Title };
                
                if (!string.IsNullOrEmpty(request.Department))
                    attributes["department"] = new[] { request.Department };
                
                if (request.Gender.HasValue)
                    attributes["gender"] = new[] { request.Gender.Value.ToString() };
                
                if (!string.IsNullOrEmpty(request.PhoneNumber))
                    attributes["phoneNumber"] = new[] { request.PhoneNumber };
                
                if (!string.IsNullOrEmpty(request.PhotoUrl))
                    attributes["photoUrl"] = new[] { request.PhotoUrl };

                if (attributes.Any())
                    userData["attributes"] = attributes;

                var json = JsonSerializer.Serialize(userData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

                var response = await _httpClient.PutAsync(BuildEndpointPath($"admin/realms/{realmName}/users/{userId}"), content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to update user {UserId} in realm {RealmName}. Status: {StatusCode}, Error: {Error}", 
                        userId, realmName, response.StatusCode, errorContent);
                    return false;
                }

                _logger.LogInformation("User updated successfully: {UserId} in realm {RealmName}", userId, realmName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {UserId} in realm {RealmName}", userId, realmName);
                return false;
            }
        }

        public async Task<bool> ValidateUserPasswordAsync(string realmName, string username, string password)
        {
            try
            {
                _logger.LogDebug("Validating password for user {Username} in realm {RealmName}", username, realmName);

                // Try to get token with provided credentials
                var tokenResponse = await GetTokenAsync(realmName, username, password);
                
                // If token is obtained successfully, password is valid
                return string.IsNullOrEmpty(tokenResponse.Error);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error validating password for user {Username} in realm {RealmName}", username, realmName);
                return false;
            }
        }

        public async Task<KeycloakRealmUserSnapshot?> GetRealmUserByUsernameAsync(
            string realmName,
            string username,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(username))
                return null;

            await EnsureAdminTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

            var encoded = Uri.EscapeDataString(username.Trim());
            var response = await _httpClient.GetAsync(
                BuildEndpointPath($"admin/realms/{realmName}/users?username={encoded}&exact=true"),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "GetRealmUserByUsername failed for {Username} realm {Realm}: {Status}",
                    username, realmName, response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var users = JsonSerializer.Deserialize<JsonElement[]>(json) ?? Array.Empty<JsonElement>();
            foreach (var el in users)
            {
                var snapshot = MapUserSnapshot(el);
                if (string.Equals(snapshot.Username, username.Trim(), StringComparison.OrdinalIgnoreCase))
                    return snapshot;
            }

            return null;
        }

        public async Task<IReadOnlyList<KeycloakRealmUserSnapshot>> ListRealmUsersAsync(
            string realmName,
            CancellationToken cancellationToken = default)
        {
            await EnsureAdminTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

            const int pageSize = 100;
            var totalCount = await GetRealmResourceCountAsync(realmName, "users", cancellationToken);
            var all = new List<KeycloakRealmUserSnapshot>();
            var first = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (totalCount >= 0 && first >= totalCount)
                    break;

                var max = pageSize;
                if (totalCount >= 0)
                    max = Math.Min(pageSize, totalCount - first);

                if (max <= 0)
                    break;

                var path = $"admin/realms/{realmName}/users?first={first}&max={max}";
                var page = await FetchRealmUserPageAsync(realmName, path, first, max, cancellationToken);
                if (page.Length == 0)
                    break;

                foreach (var el in page)
                    all.Add(MapUserSnapshot(el));

                first += page.Length;
                if (page.Length < max)
                    break;
            }

            _logger.LogInformation(
                "Listed {Count} users from Keycloak realm {Realm} (reportedTotal={Total})",
                all.Count, realmName, totalCount >= 0 ? totalCount.ToString() : "unknown");
            return all;
        }

        public async Task<IReadOnlyList<KeycloakRealmGroupSnapshot>> ListRealmGroupsAsync(
            string realmName,
            CancellationToken cancellationToken = default)
        {
            await EnsureAdminTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

            const int pageSize = 100;
            var totalCount = await GetRealmResourceCountAsync(realmName, "groups", cancellationToken);
            var all = new List<KeycloakRealmGroupSnapshot>();
            var first = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (totalCount >= 0 && first >= totalCount)
                    break;

                var max = pageSize;
                if (totalCount >= 0)
                    max = Math.Min(pageSize, totalCount - first);

                if (max <= 0)
                    break;

                var path = $"admin/realms/{realmName}/groups?first={first}&max={max}";
                var page = await FetchRealmGroupPageAsync(realmName, path, first, max, cancellationToken);
                if (page.Length == 0)
                    break;

                foreach (var el in page)
                    all.Add(MapGroupSnapshot(el));

                first += page.Length;
                if (page.Length < max)
                    break;
            }

            _logger.LogInformation(
                "Listed {Count} groups from Keycloak realm {Realm} (reportedTotal={Total})",
                all.Count, realmName, totalCount >= 0 ? totalCount.ToString() : "unknown");
            return all;
        }

        public async Task<IReadOnlyList<string>> GetUserGroupNamesAsync(
            string realmName,
            string userId,
            CancellationToken cancellationToken = default)
        {
            await EnsureAdminTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

            var response = await _httpClient.GetAsync(
                BuildEndpointPath($"admin/realms/{realmName}/users/{userId}/groups"),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GetUserGroupNames failed for user {UserId} realm {Realm}: {Status}",
                    userId, realmName, response.StatusCode);
                return Array.Empty<string>();
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var groups = JsonSerializer.Deserialize<JsonElement[]>(json) ?? Array.Empty<JsonElement>();
            return groups
                .Select(g => g.TryGetProperty("name", out var n) ? n.GetString() : null)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .ToList();
        }

        public async Task<KeycloakUserPhotoData?> GetRealmUserPhotoAsync(
            string realmName,
            string keycloakUserId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(keycloakUserId))
                return null;

            await EnsureAdminTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

            var fromAttributes = await TryReadPhotoFromUserAttributesAsync(
                realmName, keycloakUserId, cancellationToken);
            if (fromAttributes != null)
                return fromAttributes;

            return await TryReadPhotoFromProfilePictureEndpointAsync(
                realmName, keycloakUserId, cancellationToken);
        }

        private async Task<KeycloakUserPhotoData?> TryReadPhotoFromUserAttributesAsync(
            string realmName,
            string keycloakUserId,
            CancellationToken cancellationToken)
        {
            var response = await _httpClient.GetAsync(
                BuildEndpointPath($"admin/realms/{realmName}/users/{keycloakUserId}"),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("attributes", out var attributes)
                || attributes.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (var attr in attributes.EnumerateObject())
            {
                if (attr.Value.ValueKind != JsonValueKind.Array || attr.Value.GetArrayLength() == 0)
                    continue;

                var raw = attr.Value[0].GetString();
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                if (!IsLikelyPhotoAttributeName(attr.Name))
                    continue;

                var decoded = TryDecodePhotoValue(raw);
                if (decoded != null)
                    return decoded;
            }

            return null;
        }

        private async Task<KeycloakUserPhotoData?> TryReadPhotoFromProfilePictureEndpointAsync(
            string realmName,
            string keycloakUserId,
            CancellationToken cancellationToken)
        {
            var response = await _httpClient.GetAsync(
                BuildEndpointPath($"admin/realms/{realmName}/users/{keycloakUserId}/profile/picture"),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (bytes.Length == 0)
                return null;

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            return new KeycloakUserPhotoData
            {
                Bytes = bytes,
                ContentType = contentType,
                Extension = contentType.Contains("png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg",
            };
        }

        private static bool IsLikelyPhotoAttributeName(string attributeName)
        {
            return attributeName.Equals("thumbnailPhoto", StringComparison.OrdinalIgnoreCase)
                || attributeName.Equals("jpegPhoto", StringComparison.OrdinalIgnoreCase)
                || attributeName.Equals("photo", StringComparison.OrdinalIgnoreCase)
                || attributeName.Equals("picture", StringComparison.OrdinalIgnoreCase);
        }

        private static KeycloakUserPhotoData? TryDecodePhotoValue(string raw)
        {
            var trimmed = raw.Trim();
            byte[]? bytes = null;
            string contentType = "image/jpeg";

            if (trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var comma = trimmed.IndexOf(',');
                if (comma > 0)
                {
                    var meta = trimmed[..comma];
                    if (meta.Contains("png", StringComparison.OrdinalIgnoreCase))
                        contentType = "image/png";
                    else if (meta.Contains("webp", StringComparison.OrdinalIgnoreCase))
                        contentType = "image/webp";

                    try
                    {
                        bytes = Convert.FromBase64String(trimmed[(comma + 1)..]);
                    }
                    catch (FormatException)
                    {
                        return null;
                    }
                }
            }
            else
            {
                try
                {
                    bytes = Convert.FromBase64String(trimmed);
                }
                catch (FormatException)
                {
                    return null;
                }
            }

            if (bytes == null || bytes.Length == 0)
                return null;

            if (bytes.Length >= 4 && bytes[0] == 0x89 && bytes[1] == 0x50)
                contentType = "image/png";
            else if (bytes.Length >= 12
                && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46)
                contentType = "image/webp";

            return new KeycloakUserPhotoData
            {
                Bytes = bytes,
                ContentType = contentType,
                Extension = contentType switch
                {
                    "image/png" => ".png",
                    "image/webp" => ".webp",
                    _ => ".jpg",
                },
            };
        }

        private async Task<int> GetRealmResourceCountAsync(
            string realmName,
            string resource,
            CancellationToken cancellationToken)
        {
            var response = await _httpClient.GetAsync(
                BuildEndpointPath($"admin/realms/{realmName}/{resource}/count"),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Keycloak {Resource} count unavailable for realm {Realm}. Status={Status}, Error={Error}",
                    resource, realmName, response.StatusCode, error);
                return -1;
            }

            var json = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();
            if (int.TryParse(json, out var plainCount))
                return plainCount;

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Number)
                    return doc.RootElement.GetInt32();

                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("count", out var countProp)
                    && countProp.ValueKind == JsonValueKind.Number)
                {
                    return countProp.GetInt32();
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Keycloak {Resource} count response not parseable for realm {Realm}: {Json}",
                    resource, realmName, json);
            }

            return -1;
        }

        private async Task<JsonElement[]> FetchRealmUserPageAsync(
            string realmName,
            string path,
            int first,
            int max,
            CancellationToken cancellationToken)
        {
            var response = await _httpClient.GetAsync(BuildEndpointPath(path), cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "ListRealmUsers failed for {Realm} first={First} max={Max}. Status={Status}, Error={Error}",
                    realmName, first, max, response.StatusCode, json);
                throw new InvalidOperationException($"Keycloak list users failed: {response.StatusCode}");
            }

            try
            {
                return JsonSerializer.Deserialize<JsonElement[]>(json) ?? Array.Empty<JsonElement>();
            }
            catch (JsonException ex)
            {
                _logger.LogError(
                    ex,
                    "Keycloak list users returned invalid JSON for realm {Realm} first={First} max={Max} bytes={Bytes}",
                    realmName, first, max, json.Length);
                throw new InvalidOperationException(
                    $"Keycloak list users returned invalid JSON (first={first}, max={max}): {ex.Message}", ex);
            }
        }

        private async Task<JsonElement[]> FetchRealmGroupPageAsync(
            string realmName,
            string path,
            int first,
            int max,
            CancellationToken cancellationToken)
        {
            var response = await _httpClient.GetAsync(BuildEndpointPath(path), cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "ListRealmGroups failed for {Realm} first={First} max={Max}. Status={Status}, Error={Error}",
                    realmName, first, max, response.StatusCode, json);
                throw new InvalidOperationException($"Keycloak list groups failed: {response.StatusCode}");
            }

            try
            {
                return JsonSerializer.Deserialize<JsonElement[]>(json) ?? Array.Empty<JsonElement>();
            }
            catch (JsonException ex)
            {
                _logger.LogError(
                    ex,
                    "Keycloak list groups returned invalid JSON for realm {Realm} first={First} max={Max} bytes={Bytes}",
                    realmName, first, max, json.Length);
                throw new InvalidOperationException(
                    $"Keycloak list groups returned invalid JSON (first={first}, max={max}): {ex.Message}", ex);
            }
        }

        private static KeycloakRealmUserSnapshot MapUserSnapshot(JsonElement el)
        {
            string? federationLink = null;
            if (el.TryGetProperty("federationLink", out var fl) && fl.ValueKind == JsonValueKind.String)
                federationLink = fl.GetString();

            return new KeycloakRealmUserSnapshot
            {
                Id = el.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
                Username = el.TryGetProperty("username", out var u) ? u.GetString() ?? string.Empty : string.Empty,
                Email = el.TryGetProperty("email", out var e) ? e.GetString() : null,
                FirstName = el.TryGetProperty("firstName", out var fn) ? fn.GetString() : null,
                LastName = el.TryGetProperty("lastName", out var ln) ? ln.GetString() : null,
                Enabled = !el.TryGetProperty("enabled", out var en) || en.GetBoolean(),
                FederationLink = federationLink
            };
        }

        private static KeycloakRealmGroupSnapshot MapGroupSnapshot(JsonElement el)
        {
            return new KeycloakRealmGroupSnapshot
            {
                Id = el.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty,
                Name = el.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty,
                Path = el.TryGetProperty("path", out var p) ? p.GetString() : null
            };
        }

    }
}
