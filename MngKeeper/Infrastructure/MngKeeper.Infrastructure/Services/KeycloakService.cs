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
        private string? _adminToken;

        public KeycloakService(ILogger<KeycloakService> logger, HttpClient httpClient, IConfiguration configuration)
        {
            _logger = logger;
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<RealmInfo> CreateRealmAsync(string realmName, DomainSettingsDto settings)
        {
            try
            {
                _logger.LogInformation("Creating realm: {RealmName}", realmName);

                await EnsureAdminTokenAsync();

                var baseUrl = _configuration["Keycloak:BaseUrl"];
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

                var response = await _httpClient.PostAsync($"{baseUrl}/admin/realms", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to create realm {RealmName}. Status: {StatusCode}, Error: {Error}", 
                        realmName, response.StatusCode, errorContent);
                    throw new Exception($"Failed to create realm: {response.StatusCode} - {errorContent}");
                }

                // Create custom client scope for domain claims
                await CreateCustomClientScopeAsync(realmName);

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

                var baseUrl = _configuration["Keycloak:BaseUrl"];
                var userData = new
                {
                    username = request.Username,
                    email = request.Email,
                    firstName = request.FirstName,
                    lastName = request.LastName,
                    enabled = true,
                    emailVerified = true,
                    attributes = new
                    {
                        domain = new[] { realmName } // Add domain attribute
                    },
                    credentials = new[]
                    {
                        new
                        {
                            type = "password",
                            value = request.Password,
                            temporary = false
                        }
                    }
                };

                var json = JsonSerializer.Serialize(userData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

                var response = await _httpClient.PostAsync($"{baseUrl}/admin/realms/{realmName}/users", content);
                
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

        public async Task<GroupInfo> CreateGroupAsync(string realmName, CreateGroupRequest request)
        {
            try
            {
                _logger.LogInformation("Creating group: {GroupName} in realm {RealmName}", request.Name, realmName);

                await EnsureAdminTokenAsync();

                var baseUrl = _configuration["Keycloak:BaseUrl"];
                var groupData = new
                {
                    name = request.Name
                };

                var json = JsonSerializer.Serialize(groupData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

                var response = await _httpClient.PostAsync($"{baseUrl}/admin/realms/{realmName}/groups", content);
                
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

                var baseUrl = _configuration["Keycloak:BaseUrl"];
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

                // First, get the group ID by name
                var groupsResponse = await _httpClient.GetAsync($"{baseUrl}/admin/realms/{realmName}/groups");
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

                // Add user to group
                var response = await _httpClient.PutAsync($"{baseUrl}/admin/realms/{realmName}/users/{userId}/groups/{groupId}", null);
                
                if (!response.IsSuccessStatusCode)
                {
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

        public async Task<bool> IsUserInGroupAsync(string realmName, string username, string groupName)
        {
            try
            {
                _logger.LogInformation("Checking if user {Username} is in group {GroupName} in realm {RealmName}", username, groupName, realmName);

                await EnsureAdminTokenAsync();

                var baseUrl = _configuration["Keycloak:BaseUrl"];
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _adminToken);

                // First, get the user by username
                var usersResponse = await _httpClient.GetAsync($"{baseUrl}/admin/realms/{realmName}/users?username={username}");
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
                var userGroupsResponse = await _httpClient.GetAsync($"{baseUrl}/admin/realms/{realmName}/users/{userId}/groups");
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

        public async Task<KeycloakTokenResponse> GetTokenAsync(string realmName, string username, string password)
        {
            try
            {
                _logger.LogInformation("Getting token for user: {Username} in realm {RealmName}", username, realmName);

                var baseUrl = _configuration["Keycloak:BaseUrl"];

                var formContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("username", username),
                    new KeyValuePair<string, string>("password", password),
                    new KeyValuePair<string, string>("grant_type", "password"),
                    new KeyValuePair<string, string>("client_id", "admin-cli"),
                    new KeyValuePair<string, string>("scope", "profile email offline_access") // offline_access for refresh token
                });

                var response = await _httpClient.PostAsync($"{baseUrl}/realms/{realmName}/protocol/openid-connect/token", formContent);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to get token for user {Username} in realm {RealmName}. Status: {StatusCode}, Error: {Error}", 
                        username, realmName, response.StatusCode, errorContent);
                    throw new Exception($"Failed to get token: {response.StatusCode} - {errorContent}");
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

                var baseUrl = _configuration["Keycloak:BaseUrl"];

                var formContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "refresh_token"),
                    new KeyValuePair<string, string>("refresh_token", refreshToken),
                    new KeyValuePair<string, string>("client_id", "admin-cli")
                });

                var response = await _httpClient.PostAsync($"{baseUrl}/realms/{realmName}/protocol/openid-connect/token", formContent);
                
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

                var baseUrl = _configuration["Keycloak:BaseUrl"];

                var formContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("token", refreshToken),
                    new KeyValuePair<string, string>("client_id", "admin-cli"),
                    new KeyValuePair<string, string>("token_type_hint", "refresh_token")
                });

                var response = await _httpClient.PostAsync($"{baseUrl}/realms/{realmName}/protocol/openid-connect/revoke", formContent);
                
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

        public async Task<bool> DeleteGroupAsync(string realmName, string groupId)
        {
            try
            {
                _logger.LogInformation("Deleting group: {GroupId} in realm {RealmName}", groupId, realmName);

                // TODO: Implement actual Keycloak integration
                await Task.Delay(100); // Simulate async operation

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting group: {GroupId} in realm {RealmName}", groupId, realmName);
                return false;
            }
        }

        private async Task EnsureAdminTokenAsync()
        {
            if (!string.IsNullOrEmpty(_adminToken))
                return;

            try
            {
                var baseUrl = _configuration["Keycloak:BaseUrl"];
                var adminUsername = _configuration["Keycloak:AdminUsername"];
                var adminPassword = _configuration["Keycloak:AdminPassword"];

                var tokenData = new
                {
                    username = adminUsername,
                    password = adminPassword,
                    grant_type = "password",
                    client_id = "admin-cli"
                };

                var formContent = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("username", adminUsername!),
                    new KeyValuePair<string, string>("password", adminPassword!),
                    new KeyValuePair<string, string>("grant_type", "password"),
                    new KeyValuePair<string, string>("client_id", "admin-cli")
                });

                var response = await _httpClient.PostAsync($"{baseUrl}/realms/master/protocol/openid-connect/token", formContent);
                
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

                var baseUrl = _configuration["Keycloak:BaseUrl"];

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

                var response = await _httpClient.PostAsync($"{baseUrl}/admin/realms/{realmName}/client-scopes", content);
                
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
                var baseUrl = _configuration["Keycloak:BaseUrl"];

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

                var response = await _httpClient.PostAsync($"{baseUrl}/admin/realms/{realmName}/client-scopes/{scopeId}/protocol-mappers/models", content);
                
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
    }
}
