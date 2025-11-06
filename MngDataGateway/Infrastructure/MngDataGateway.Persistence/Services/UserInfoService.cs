using MngDataGateway.Application.Services;
using MngDataGateway.Domain.Entities.Base;

namespace MngDataGateway.Persistence.Services;

/// <summary>
/// User Info Service Implementation
/// JWT token'dan UserInfo nesnesi oluşturur
/// </summary>
public class UserInfoService : IUserInfoService
{
    private readonly IMongoContextService _mongoContextService;

    public UserInfoService(IMongoContextService mongoContextService)
    {
        _mongoContextService = mongoContextService ?? throw new ArgumentNullException(nameof(mongoContextService));
    }

    /// <summary>
    /// Mevcut HTTP request'in JWT token'ından UserInfo oluşturur
    /// </summary>
    public UserInfo GetCurrentUserInfo()
    {
        var userId = _mongoContextService.GetCurrentUserId() 
            ?? throw new InvalidOperationException("User ID bulunamadı");
        
        var username = _mongoContextService.GetCurrentUsername() 
            ?? throw new InvalidOperationException("Username bulunamadı");
        
        var domainName = _mongoContextService.GetCurrentDomainName() 
            ?? throw new InvalidOperationException("Domain name bulunamadı");

        return new UserInfo
        {
            uid = userId,
            userName = username,
            domain = domainName
        };
    }
}

