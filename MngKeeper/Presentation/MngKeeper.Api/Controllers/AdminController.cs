using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MngKeeper.Application.Interfaces;

namespace MngKeeper.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDomainRepository _domainRepository;

    public AdminController(
        IConfiguration configuration,
        ILogger<AdminController> logger,
        IHttpClientFactory httpClientFactory,
        IDomainRepository domainRepository)
    {
        _configuration = configuration;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _domainRepository = domainRepository;
    }

    /// <summary>
    /// Configure protocol mappers (user_groups, isAdmin) for a realm's admin-cli client
    /// This is a helper endpoint to add custom claims to tokens
    /// </summary>
    /// <param name="realmName">The realm name</param>
    /// <returns>Success status</returns>
    [HttpPost("realms/{realmName}/configure-mappers")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ConfigureMappersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfigureRealmMappers(string realmName)
    {
        try
        {
            _logger.LogInformation("Configuring mappers for realm: {RealmName}", realmName);

            // Get master token
            var masterToken = await GetMasterAdminTokenAsync();
            if (string.IsNullOrEmpty(masterToken))
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "authentication_failed",
                    ErrorDescription = "Failed to authenticate with Keycloak master realm"
                });
            }

            var httpClient = _httpClientFactory.CreateClient();
            var baseUrl = _configuration["MngKeeperSettings:Keycloak:BaseUrl"];
            httpClient.BaseAddress = new Uri(baseUrl!);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", masterToken);

            // Get admin-cli client UUID
            var clientsResponse = await httpClient.GetAsync($"/admin/realms/{realmName}/clients?clientId=admin-cli");
            if (!clientsResponse.IsSuccessStatusCode)
            {
                var error = await clientsResponse.Content.ReadAsStringAsync();
                _logger.LogError("Failed to get admin-cli client. Status: {Status}, Error: {Error}", 
                    clientsResponse.StatusCode, error);
                
                return BadRequest(new ErrorResponse
                {
                    Error = "client_not_found",
                    ErrorDescription = $"admin-cli client not found in realm {realmName}"
                });
            }

            var clientsJson = await clientsResponse.Content.ReadAsStringAsync();
            var clients = JsonSerializer.Deserialize<JsonElement[]>(clientsJson);
            
            if (clients == null || clients.Length == 0)
            {
                return BadRequest(new ErrorResponse
                {
                    Error = "client_not_found",
                    ErrorDescription = "admin-cli client not found"
                });
            }

            var clientId = clients[0].GetProperty("id").GetString();

            var mappersAdded = new List<string>();

            // Add user_groups mapper
            var groupMapperAdded = await AddGroupMembershipMapperAsync(httpClient, realmName, clientId!);
            if (groupMapperAdded) mappersAdded.Add("user_groups");

            // Add isAdmin mapper
            var isAdminMapperAdded = await AddIsAdminMapperAsync(httpClient, realmName, clientId!);
            if (isAdminMapperAdded) mappersAdded.Add("isAdmin");

            // Add domain_name mapper
            var domainNameMapperAdded = await AddDomainNameMapperAsync(httpClient, realmName, clientId!);
            if (domainNameMapperAdded) mappersAdded.Add("domain_name");

            // Add domain_id mapper (requires domain lookup)
            var domainIdMapperAdded = await AddDomainIdMapperAsync(httpClient, realmName, clientId!);
            if (domainIdMapperAdded) mappersAdded.Add("domain_id");

            _logger.LogInformation("Mappers configured successfully for realm: {RealmName}. Added: {Mappers}", 
                realmName, string.Join(", ", mappersAdded));

            return Ok(new ConfigureMappersResponse
            {
                RealmName = realmName,
                MappersAdded = mappersAdded,
                Message = $"Successfully configured {mappersAdded.Count} mapper(s) for realm {realmName}"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error configuring mappers for realm: {RealmName}", realmName);
            
            return StatusCode(500, new ErrorResponse
            {
                Error = "server_error",
                ErrorDescription = "An error occurred while configuring mappers"
            });
        }
    }

    private async Task<string?> GetMasterAdminTokenAsync()
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient();
            var baseUrl = _configuration["MngKeeperSettings:Keycloak:BaseUrl"];
            
            var clientId = _configuration["MngKeeperSettings:Keycloak:ClientId"];
            var clientSecret = _configuration["MngKeeperSettings:Keycloak:ClientSecret"];
            var adminUsername = _configuration["MngKeeperSettings:Keycloak:AdminUsername"];
            var adminPassword = _configuration["MngKeeperSettings:Keycloak:AdminPassword"];

            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("username", adminUsername!),
                new KeyValuePair<string, string>("password", adminPassword!),
                new KeyValuePair<string, string>("grant_type", "password"),
                new KeyValuePair<string, string>("client_id", clientId!),
                new KeyValuePair<string, string>("client_secret", clientSecret!)
            });

            var response = await httpClient.PostAsync($"{baseUrl}/realms/master/protocol/openid-connect/token", formContent);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to get master admin token");
                return null;
            }

            var tokenJson = await response.Content.ReadAsStringAsync();
            var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenJson);
            
            return tokenData.GetProperty("access_token").GetString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting master admin token");
            return null;
        }
    }

    private async Task<bool> AddGroupMembershipMapperAsync(HttpClient httpClient, string realmName, string clientId)
    {
        try
        {
            var mapperData = new
            {
                name = "user-groups-mapper",
                protocol = "openid-connect",
                protocolMapper = "oidc-group-membership-mapper",
                config = new Dictionary<string, string>
                {
                    ["full.path"] = "false",
                    ["id.token.claim"] = "true",
                    ["access.token.claim"] = "true",
                    ["claim.name"] = "user_groups",
                    ["userinfo.token.claim"] = "true"
                }
            };

            var json = JsonSerializer.Serialize(mapperData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync($"/admin/realms/{realmName}/clients/{clientId}/protocol-mappers/models", content);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to add user_groups mapper. Error: {Error}", error);
                return false;
            }

            _logger.LogInformation("user_groups mapper added successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding user_groups mapper");
            return false;
        }
    }

    private async Task<bool> AddIsAdminMapperAsync(HttpClient httpClient, string realmName, string clientId)
    {
        try
        {
            var mapperData = new
            {
                name = "isAdmin-mapper",
                protocol = "openid-connect",
                protocolMapper = "oidc-usermodel-attribute-mapper",
                config = new Dictionary<string, string>
                {
                    ["user.attribute"] = "isAdmin",
                    ["id.token.claim"] = "true",
                    ["access.token.claim"] = "true",
                    ["userinfo.token.claim"] = "true",
                    ["claim.name"] = "isAdmin",
                    ["jsonType.label"] = "boolean"
                }
            };

            var json = JsonSerializer.Serialize(mapperData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync($"/admin/realms/{realmName}/clients/{clientId}/protocol-mappers/models", content);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to add isAdmin mapper. Error: {Error}", error);
                return false;
            }

            _logger.LogInformation("isAdmin mapper added successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding isAdmin mapper");
            return false;
        }
    }

    private async Task<bool> AddDomainNameMapperAsync(HttpClient httpClient, string realmName, string clientId)
    {
        try
        {
            // Hardcoded claim mapper - domain name is the realm name
            var mapperData = new
            {
                name = "domain-name-mapper",
                protocol = "openid-connect",
                protocolMapper = "oidc-hardcoded-claim-mapper",
                config = new Dictionary<string, string>
                {
                    ["claim.name"] = "domain_name",
                    ["claim.value"] = realmName,
                    ["id.token.claim"] = "true",
                    ["access.token.claim"] = "true",
                    ["userinfo.token.claim"] = "true",
                    ["jsonType.label"] = "String"
                }
            };

            var json = JsonSerializer.Serialize(mapperData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync($"/admin/realms/{realmName}/clients/{clientId}/protocol-mappers/models", content);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to add domain_name mapper. Error: {Error}", error);
                return false;
            }

            _logger.LogInformation("domain_name mapper added successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding domain_name mapper");
            return false;
        }
    }

    private async Task<bool> AddDomainIdMapperAsync(HttpClient httpClient, string realmName, string clientId)
    {
        try
        {
            // Get domain ID from domain name (realm name)
            var domain = await _domainRepository.GetByNameAsync(realmName);
            if (domain == null)
            {
                _logger.LogWarning("Domain not found for realm: {RealmName}, skipping domain_id mapper", realmName);
                return false;
            }

            var mapperData = new
            {
                name = "domain-id-mapper",
                protocol = "openid-connect",
                protocolMapper = "oidc-hardcoded-claim-mapper",
                config = new Dictionary<string, string>
                {
                    ["claim.name"] = "domain_id",
                    ["claim.value"] = domain.Id,
                    ["id.token.claim"] = "true",
                    ["access.token.claim"] = "true",
                    ["userinfo.token.claim"] = "true",
                    ["jsonType.label"] = "String"
                }
            };

            var json = JsonSerializer.Serialize(mapperData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync($"/admin/realms/{realmName}/clients/{clientId}/protocol-mappers/models", content);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to add domain_id mapper. Error: {Error}", error);
                return false;
            }

            _logger.LogInformation("domain_id mapper added successfully with domain ID: {DomainId}", domain.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding domain_id mapper");
            return false;
        }
    }
}

// Response DTOs
public class ConfigureMappersResponse
{
    public string RealmName { get; set; } = string.Empty;
    public List<string> MappersAdded { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}

