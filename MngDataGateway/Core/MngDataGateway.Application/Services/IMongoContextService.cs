using MongoDB.Driver;

namespace MngDataGateway.Application.Services;

/// <summary>
/// MongoDB Context Service - JWT token'dan domain bilgisini alarak doğru database'i seçer
/// </summary>
public interface IMongoContextService
{
    /// <summary>
    /// Mevcut request'in JWT token'ından domain bilgisini alarak ilgili database'i döner
    /// </summary>
    /// <returns>IMongoDatabase instance (mng_{domain_name})</returns>
    IMongoDatabase GetDatabase();
    
    /// <summary>
    /// Belirtilen domain name için database döner
    /// </summary>
    /// <param name="domainName">Domain adı (örn: "seven")</param>
    /// <returns>IMongoDatabase instance (mng_{domain_name})</returns>
    IMongoDatabase GetDatabase(string domainName);
    
    /// <summary>
    /// Mevcut kullanıcının JWT token'ından domain adını alır
    /// </summary>
    /// <returns>Domain name (örn: "seven")</returns>
    string? GetCurrentDomainName();
    
    /// <summary>
    /// Mevcut kullanıcının JWT token'ından user ID'yi alır
    /// </summary>
    /// <returns>User ID (MongoDB ObjectId string)</returns>
    string? GetCurrentUserId();
    
    /// <summary>
    /// Mevcut kullanıcının JWT token'ından username'i alır
    /// </summary>
    /// <returns>Username (preferred_username claim)</returns>
    string? GetCurrentUsername();
    
    /// <summary>
    /// Mevcut kullanıcının admin olup olmadığını kontrol eder
    /// </summary>
    /// <returns>True ise admin, false değilse normal user</returns>
    bool IsCurrentUserAdmin();
}

