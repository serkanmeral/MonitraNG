using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MngDataGateway.Application.Services;
using MongoDB.Bson;
using MongoDB.Driver;

namespace MngDataGateway.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MongoContextTestController : ControllerBase
{
    private readonly IMongoContextService _mongoContextService;
    private readonly ILogger<MongoContextTestController> _logger;

    public MongoContextTestController(
        IMongoContextService mongoContextService,
        ILogger<MongoContextTestController> logger)
    {
        _mongoContextService = mongoContextService;
        _logger = logger;
    }

    /// <summary>
    /// Test MongoContextService - Domain ve User bilgilerini döner
    /// </summary>
    /// <returns>Domain ve user bilgileri</returns>
    [HttpGet("info")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetContextInfo()
    {
        var domainName = _mongoContextService.GetCurrentDomainName();
        var userId = _mongoContextService.GetCurrentUserId();
        var username = _mongoContextService.GetCurrentUsername();
        var isAdmin = _mongoContextService.IsCurrentUserAdmin();

        var result = new
        {
            DomainName = domainName,
            DatabaseName = string.IsNullOrWhiteSpace(domainName) ? null : $"mng_{domainName}",
            UserId = userId,
            Username = username,
            IsAdmin = isAdmin,
            Message = "JWT token'dan başarıyla bilgiler çıkarıldı"
        };

        _logger.LogInformation(
            "Context Info - Domain: {Domain}, User: {Username}, IsAdmin: {IsAdmin}",
            domainName, username, isAdmin);

        return Ok(result);
    }

    /// <summary>
    /// Test MongoContextService - Mevcut domain'in database'ine erişim testi
    /// </summary>
    /// <returns>Database bilgileri ve collections listesi</returns>
    [HttpGet("database")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDatabaseInfo()
    {
        try
        {
            var database = _mongoContextService.GetDatabase();
            var domainName = _mongoContextService.GetCurrentDomainName();

            // Collection listesini al
            var collections = await database.ListCollectionNames().ToListAsync();

            var result = new
            {
                DomainName = domainName,
                DatabaseName = database.DatabaseNamespace.DatabaseName,
                CollectionsCount = collections.Count,
                Collections = collections,
                Message = "Database'e başarıyla erişildi"
            };

            _logger.LogInformation(
                "Database Info - Domain: {Domain}, DB: {Database}, Collections: {Count}",
                domainName, database.DatabaseNamespace.DatabaseName, collections.Count);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "JWT token'da domain_name claim'i bulunamadı");
            return BadRequest(new { Error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database bilgisi alınırken hata oluştu");
            return StatusCode(500, new { Error = "Database'e erişimde hata oluştu", Details = ex.Message });
        }
    }

    /// <summary>
    /// Test MongoContextService - @datasets collection'ını okuma testi
    /// </summary>
    /// <returns>@datasets collection'ındaki kayıt sayısı</returns>
    [HttpGet("datasets-collection")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetDatasetsCollection()
    {
        try
        {
            var database = _mongoContextService.GetDatabase();
            var domainName = _mongoContextService.GetCurrentDomainName();

            // @datasets collection'ına eriş
            var datasetsCollection = database.GetCollection<BsonDocument>("@datasets");
            var count = await datasetsCollection.CountDocumentsAsync(new BsonDocument());

            // İlk 5 kaydı al (varsa)
            var datasets = await datasetsCollection
                .Find(new BsonDocument())
                .Limit(5)
                .ToListAsync();

            var result = new
            {
                DomainName = domainName,
                DatabaseName = database.DatabaseNamespace.DatabaseName,
                CollectionName = "@datasets",
                TotalCount = count,
                SampleCount = datasets.Count,
                Samples = datasets.Select(d => new
                {
                    DataId = d.GetValue("__dataId", "N/A").ToString(),
                    Name = d.GetValue("name", "N/A").ToString(),
                    Description = d.GetValue("description", "N/A").ToString()
                }),
                Message = "@datasets collection'ına başarıyla erişildi"
            };

            _logger.LogInformation(
                "Datasets Collection - Domain: {Domain}, Total: {Count}",
                domainName, count);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "@datasets collection'ına erişimde hata oluştu");
            return StatusCode(500, new { Error = "@datasets collection'ına erişimde hata", Details = ex.Message });
        }
    }

    /// <summary>
    /// Test MongoContextService - Belirli bir domain için database erişimi
    /// </summary>
    /// <param name="domainName">Domain adı (örn: seven)</param>
    /// <returns>Belirtilen domain'in database bilgileri</returns>
    [HttpGet("database/{domainName}")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDatabaseByDomain(string domainName)
    {
        try
        {
            // Admin kontrolü (opsiyonel)
            if (!_mongoContextService.IsCurrentUserAdmin())
            {
                return Forbid("Bu endpoint sadece admin kullanıcılar tarafından kullanılabilir");
            }

            var database = _mongoContextService.GetDatabase(domainName);
            var collections = await database.ListCollectionNames().ToListAsync();

            var result = new
            {
                RequestedDomain = domainName,
                DatabaseName = database.DatabaseNamespace.DatabaseName,
                CollectionsCount = collections.Count,
                Collections = collections,
                Message = "Belirtilen domain'in database'ine erişildi"
            };

            _logger.LogInformation(
                "Database by Domain - Requested: {Domain}, DB: {Database}",
                domainName, database.DatabaseNamespace.DatabaseName);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Domain {Domain} için database erişiminde hata", domainName);
            return StatusCode(500, new { Error = "Database'e erişimde hata", Details = ex.Message });
        }
    }

    /// <summary>
    /// Health check - MongoContextService durumu
    /// </summary>
    /// <returns>Service sağlık durumu</returns>
    [HttpGet("health")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Health()
    {
        try
        {
            var domainName = _mongoContextService.GetCurrentDomainName();
            var isHealthy = !string.IsNullOrWhiteSpace(domainName);

            return Ok(new
            {
                Service = "MongoContextService",
                Status = isHealthy ? "Healthy" : "Unhealthy",
                HasDomain = isHealthy,
                DomainName = domainName,
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return Ok(new
            {
                Service = "MongoContextService",
                Status = "Unhealthy",
                Error = ex.Message,
                Timestamp = DateTime.UtcNow
            });
        }
    }
}

