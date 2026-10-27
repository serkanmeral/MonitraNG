using MngKeeper.Domain.Entities;

namespace MngKeeper.Application.Directory;

/// <summary>
/// Self-service writes the signed-in user's own Active Directory password.
/// The read-only directory bind only finds the account. The user bind changes the password.
/// A personnel container and a reset account are not required.
/// Admin reset stays closed until those two are filled.
/// </summary>
public static class DirectoryPasswordPolicy
{
    public static bool IsAdWriteEnabled(DomainSettings? settings)
    {
        var password = settings?.Password;
        var ldap = settings?.DirectoryLdap;
        if (password == null || ldap == null)
            return false;

        if (!string.Equals(password.Mode?.Trim(), DomainPasswordModes.KeycloakAndAd, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!ldap.Enabled || !ldap.UseSsl)
            return false;

        return !string.IsNullOrWhiteSpace(ldap.Host)
            && !string.IsNullOrWhiteSpace(ldap.BaseDn)
            && !string.IsNullOrWhiteSpace(ldap.BindUsername)
            && !string.IsNullOrWhiteSpace(ldap.BindPassword)
            && !string.IsNullOrWhiteSpace(password.ServerCertificateThumbprint);
    }

    public static bool IsAdminResetEnabled(DomainSettings? settings)
    {
        if (!IsAdWriteEnabled(settings))
            return false;

        var password = settings!.Password!;
        return !string.IsNullOrWhiteSpace(password.PersonnelDn)
            && !string.IsNullOrWhiteSpace(password.ResetBindUsername)
            && !string.IsNullOrWhiteSpace(password.ResetBindPassword);
    }
}
