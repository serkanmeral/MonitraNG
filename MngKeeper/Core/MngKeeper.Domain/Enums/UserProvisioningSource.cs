namespace MngKeeper.Domain.Enums;

/// <summary>
/// Kullanıcının MonitraNG'de nasıl oluşturulduğu / kim tarafından yönetildiği.
/// </summary>
public enum UserProvisioningSource
{
    /// <summary>Yerel kullanıcı (POST /api/user, domain admin). Directory sync güncellemez.</summary>
    Local = 0,

    /// <summary>Keycloak federation (LDAP/AD) üzerinden K2/K3/K4 sync ile oluşturulmuş.</summary>
    Directory = 1
}
