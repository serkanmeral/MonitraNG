using Microsoft.AspNetCore.Mvc;
using MngKeeper.Application.Interfaces;
using MngKeeper.Api.Attributes;
using System.Text;
using System.Text.Json;

namespace MngKeeper.Api.Controllers;

/// <summary>
/// System locales (language files) management controller
/// </summary>
[ApiController]
[Route("api/system/locales")]
[Produces("application/json")]
[ApiExplorerSettings(GroupName = "System Management")]
public class SystemLocalesController : ControllerBase
{
    private const string SystemBucketName = "system";
    private const string LocalesFolderPath = "System/locales/";
    
    private readonly IMinioService _minioService;
    private readonly ILogger<SystemLocalesController> _logger;

    public SystemLocalesController(
        IMinioService minioService,
        ILogger<SystemLocalesController> logger)
    {
        _minioService = minioService;
        _logger = logger;
    }

    /// <summary>
    /// Gets a locale file (language JSON) by locale code
    /// </summary>
    /// <param name="locale">Locale code (e.g., 'tr', 'en', 'ar')</param>
    /// <returns>Locale file content as JSON</returns>
    /// <response code="200">Locale file found and returned</response>
    /// <response code="404">Locale file not found</response>
    [HttpGet("{locale}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLocale(string locale)
    {
        try
        {
            // Ensure system bucket exists
            await EnsureSystemBucketExistsAsync();

            var objectName = $"{LocalesFolderPath}{locale}.json";
            
            var stream = await _minioService.GetObjectAsync(SystemBucketName, objectName);
            
            if (stream == null)
            {
                _logger.LogInformation("Locale file not found: {Locale}", locale);
                return NotFound(new { error = "Locale file not found", locale });
            }

            using (stream)
            {
                using var reader = new StreamReader(stream, Encoding.UTF8);
                var jsonContent = await reader.ReadToEndAsync();
                
                // Parse JSON to validate it
                var jsonObject = JsonSerializer.Deserialize<object>(jsonContent);
                
                return Ok(jsonObject);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting locale file: {Locale}", locale);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Updates or creates a locale file (language JSON)
    /// </summary>
    /// <param name="locale">Locale code (e.g., 'tr', 'en', 'ar')</param>
    /// <param name="localeData">Locale file content as JSON object</param>
    /// <returns>Success status</returns>
    /// <response code="200">Locale file updated successfully</response>
    /// <response code="400">Invalid locale data</response>
    [HttpPut("{locale}")]
    [ManagerOrAdminAuthorization]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PutLocale(string locale, [FromBody] object localeData)
    {
        try
        {
            // Ensure system bucket exists
            await EnsureSystemBucketExistsAsync();

            // Validate locale code (basic validation)
            if (string.IsNullOrWhiteSpace(locale) || locale.Length > 10)
            {
                return BadRequest(new { error = "Invalid locale code" });
            }

            // Validate locale data
            if (localeData == null)
            {
                return BadRequest(new { error = "Locale data is required" });
            }

            // Serialize JSON
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            
            var jsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(localeData, jsonOptions));
            using var stream = new MemoryStream(jsonBytes);

            var objectName = $"{LocalesFolderPath}{locale}.json";
            
            var success = await _minioService.PutObjectAsync(
                SystemBucketName, 
                objectName, 
                stream, 
                "application/json"
            );

            if (!success)
            {
                _logger.LogError("Failed to save locale file: {Locale}", locale);
                return StatusCode(500, new { error = "Failed to save locale file" });
            }

            _logger.LogInformation("Locale file saved successfully: {Locale}", locale);
            return Ok(new { message = "Locale file saved successfully", locale });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in locale data: {Locale}", locale);
            return BadRequest(new { error = "Invalid JSON format", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving locale file: {Locale}", locale);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Lists all available locale files
    /// </summary>
    /// <returns>List of available locale codes</returns>
    /// <response code="200">List of locales returned</response>
    [HttpGet]
    [ProducesResponseType(typeof(string[]), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLocales()
    {
        try
        {
            // For now, return known locales
            // In the future, this could list objects from MinIO
            var knownLocales = new[] { "tr", "en", "fr", "ar", "zh" };
            
            return Ok(knownLocales);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing locales");
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Ensures the system bucket exists, creates it if it doesn't
    /// </summary>
    private async Task EnsureSystemBucketExistsAsync()
    {
        var bucketExists = await _minioService.BucketExistsAsync(SystemBucketName);
        if (!bucketExists)
        {
            _logger.LogInformation("System bucket does not exist, creating: {BucketName}", SystemBucketName);
            await _minioService.CreateBucketAsync(SystemBucketName);
            
            // Create folder structure
            var folders = new[] { "System/locales" };
            await _minioService.CreateFolderStructureAsync(SystemBucketName, folders);
            
            _logger.LogInformation("System bucket created successfully: {BucketName}", SystemBucketName);
        }
    }
}
