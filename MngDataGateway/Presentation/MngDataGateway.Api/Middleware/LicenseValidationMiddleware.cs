using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using MngDataGateway.Application.Configuration;
using MngDataGateway.Application.Services;
using Microsoft.Extensions.Options;
using System.Text;

namespace MngDataGateway.Api.Middleware;

/// <summary>
/// Middleware to validate license for domain operations
/// Blocks CRUD and GET operations if license is expired
/// </summary>
public class LicenseValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LicenseValidationMiddleware> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MngDataGatewaySettings _settings;

    public LicenseValidationMiddleware(
        RequestDelegate next,
        ILogger<LicenseValidationMiddleware> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<MngDataGatewaySettings> settings)
    {
        _next = next;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip license check for health check and other non-data endpoints
        var path = context.Request.Path.Value?.ToLower() ?? "";
        if (path.Contains("/health") || 
            path.Contains("/swagger") || 
            path.Contains("/api-docs") ||
            !path.Contains("/api/v") ||
            !path.Contains("/data/"))
        {
            await _next(context);
            return;
        }

        try
        {
            // Get scoped service from HttpContext
            var mongoContextService = context.RequestServices.GetRequiredService<IMongoContextService>();
            var domainName = mongoContextService.GetCurrentDomainName();
            if (string.IsNullOrEmpty(domainName))
            {
                // No domain in token, let it pass (will be handled by auth)
                await _next(context);
                return;
            }

            // Determine operation type from HTTP method and path
            var operation = DetermineOperation(context.Request.Method, path);
            if (operation == null)
            {
                // Not a data operation, skip
                await _next(context);
                return;
            }

            // Check license via MngKeeper API
            var isAllowed = await CheckLicenseOperationAsync(domainName, operation.Value);
            
            if (!isAllowed)
            {
                _logger.LogWarning(
                    "License check failed for domain: {DomainName}, operation: {Operation}, path: {Path}",
                    domainName, operation, path);

                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                context.Response.ContentType = "application/json";

                var errorResponse = new
                {
                    error = "LICENSE_EXPIRED",
                    message = "Lisans süreniz dolmuştur. Lütfen lisansınızı yenileyin.",
                    domainName = domainName,
                    operation = operation.ToString()
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(errorResponse));
                return;
            }

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in license validation middleware");
            // On error, allow request to proceed (fail open for availability)
            await _next(context);
        }
    }

    private LicenseOperation? DetermineOperation(string httpMethod, string path)
    {
        // CRUD operations
        if (httpMethod == "POST" && path.Contains("/data/"))
        {
            if (path.Contains("/bulk"))
                return LicenseOperation.CrudOperation;
            if (path.Contains("/query") || path.Contains("/aggregate"))
                return LicenseOperation.GetOperation;
            return LicenseOperation.CrudOperation; // Create
        }
        
        if (httpMethod == "PUT" || httpMethod == "PATCH")
            return LicenseOperation.CrudOperation; // Update
        
        if (httpMethod == "DELETE")
            return LicenseOperation.CrudOperation; // Delete
        
        // GET operations
        if (httpMethod == "GET" && path.Contains("/data/"))
            return LicenseOperation.GetOperation;

        return null;
    }

    private async Task<bool> CheckLicenseOperationAsync(string domainName, LicenseOperation operation)
    {
        try
        {
            var keeperUrl = _settings.Actors?.MngKeeper;
            if (string.IsNullOrEmpty(keeperUrl))
            {
                _logger.LogWarning("MngKeeper URL not configured, skipping license check");
                return true; // Fail open
            }

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5); // Short timeout for license check

            // Map to MngKeeper's LicenseOperation enum values:
            // 0 = TokenGeneration, 1 = CrudOperation, 2 = GetOperation
            int operationValue = operation switch
            {
                LicenseOperation.CrudOperation => 1,
                LicenseOperation.GetOperation => 2,
                _ => 1 // Default to CrudOperation
            };

            // MngKeeper expects PascalCase property names
            var request = new
            {
                DomainName = domainName,
                Operation = operationValue
            };

            // Use default JSON serialization (PascalCase) to match MngKeeper's DTO
            var json = JsonSerializer.Serialize(request, new JsonSerializerOptions { PropertyNamingPolicy = null });
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            // Note: License check endpoint should be public or use service-to-service auth
            // For now, we'll call without auth (MngKeeper should have internal endpoint)

            var url = $"{keeperUrl}/api/license/check-operation";
            _logger.LogInformation(
                "Checking license for domain: {DomainName}, operation: {Operation} ({OperationValue}), URL: {Url}, RequestBody: {RequestBody}",
                domainName, operation, operationValue, url, json);

            var response = await httpClient.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning(
                    "License check failed with status {StatusCode} for domain: {DomainName}, operation: {Operation}, URL: {Url}, RequestBody: {RequestBody}, Response: {Response}",
                    response.StatusCode, domainName, operation, url, json, errorContent);
                return false;
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            
            // MngKeeper returns camelCase JSON, parse manually to avoid deserialization issues
            bool isAllowed = false;
            try
            {
                using (JsonDocument doc = JsonDocument.Parse(responseJson))
                {
                    if (doc.RootElement.TryGetProperty("isAllowed", out var isAllowedElement))
                    {
                        isAllowed = isAllowedElement.GetBoolean();
                        _logger.LogInformation(
                            "Parsed license check response: isAllowed={IsAllowed} from JSON: {ResponseJson}",
                            isAllowed, responseJson);
                    }
                    else
                    {
                        _logger.LogWarning("License check response missing 'isAllowed' property: {ResponseJson}", responseJson);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse license check response: {ResponseJson}", responseJson);
                return false; // Fail closed on parse error
            }

            _logger.LogInformation(
                "License check result for domain: {DomainName}, operation: {Operation}: {IsAllowed}, ResponseBody: {ResponseBody}",
                domainName, operation, isAllowed, responseJson);

            return isAllowed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking license operation for domain: {DomainName}", domainName);
            return true; // Fail open - allow request if license check fails
        }
    }

    private enum LicenseOperation
    {
        CrudOperation,
        GetOperation
    }

    private class LicenseCheckResponse
    {
        [JsonPropertyName("isAllowed")]
        public bool IsAllowed { get; set; }
        
        [JsonPropertyName("domainName")]
        public string DomainName { get; set; } = string.Empty;
        
        [JsonPropertyName("operation")]
        public string Operation { get; set; } = string.Empty;
    }
}
