using MngDataGateway.Domain.Entities.Base;

namespace MngDataGateway.Application.Services;

/// <summary>
/// User Info Service - JWT token'dan UserInfo nesnesi oluşturur
/// </summary>
public interface IUserInfoService
{
    /// <summary>
    /// Mevcut HTTP request'in JWT token'ından UserInfo oluşturur
    /// </summary>
    /// <returns>UserInfo instance</returns>
    UserInfo GetCurrentUserInfo();
}

