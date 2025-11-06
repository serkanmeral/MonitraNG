using MngDataGateway.Application.Services;
using Microsoft.AspNetCore.Http;
using MongoDB.Driver;
using System.Security.Claims;

namespace MngDataGateway.Persistence.Services;

/// <summary>
/// MongoDB Context Service Implementation
/// JWT token'dan domain bilgisini alarak doğru database'i seçer
/// </summary>
public class MongoContextService : IMongoContextService
{
    private readonly IMongoClient _mongoClient;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MongoContextService(IMongoClient mongoClient, IHttpContextAccessor httpContextAccessor)
    {
        _mongoClient = mongoClient ?? throw new ArgumentNullException(nameof(mongoClient));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    /// <summary>
    /// Mevcut request'in JWT token'ından domain bilgisini alarak ilgili database'i döner
    /// </summary>
    public IMongoDatabase GetDatabase()
    {
        var domainName = GetCurrentDomainName();
        
        if (string.IsNullOrWhiteSpace(domainName))
        {
            throw new InvalidOperationException("JWT token'da domain_name claim'i bulunamadı.");
        }

        return GetDatabase(domainName);
    }

    /// <summary>
    /// Belirtilen domain name için database döner
    /// </summary>
    public IMongoDatabase GetDatabase(string domainName)
    {
        if (string.IsNullOrWhiteSpace(domainName))
        {
            throw new ArgumentException("Domain name boş olamaz.", nameof(domainName));
        }

        // Database adı: mng_{domain_name} formatında
        var databaseName = $"mng_{domainName}";
        
        return _mongoClient.GetDatabase(databaseName);
    }

    /// <summary>
    /// Mevcut kullanıcının JWT token'ından domain adını alır
    /// </summary>
    public string? GetCurrentDomainName()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return user?.FindFirst("domain_name")?.Value;
    }

    /// <summary>
    /// Mevcut kullanıcının JWT token'ından user ID'yi alır
    /// </summary>
    public string? GetCurrentUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        
        // ClaimTypes.NameIdentifier veya "sub" claim'ine bakıyoruz
        return user?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? user?.FindFirst("sub")?.Value;
    }

    /// <summary>
    /// Mevcut kullanıcının JWT token'ından username'i alır
    /// </summary>
    public string? GetCurrentUsername()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        
        // preferred_username claim'ine bakıyoruz
        return user?.FindFirst("preferred_username")?.Value;
    }

    /// <summary>
    /// Mevcut kullanıcının admin olup olmadığını kontrol eder
    /// </summary>
    public bool IsCurrentUserAdmin()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var isAdminClaim = user?.FindFirst("isAdmin")?.Value;
        
        // isAdmin claim'i "true" string'i veya boolean true olabilir
        return isAdminClaim?.ToLowerInvariant() == "true";
    }
}

