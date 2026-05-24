namespace MngKeeper.Application.Common;

/// <summary>
/// E-posta zorunlu değil (LDAP/AD); boş değerler Mongo unique index ile çakışmaması için null saklanır.
/// </summary>
public static class UserEmailHelper
{
    public static string? NormalizeForStorage(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;
        return email.Trim();
    }

    public static bool HasValue(string? email) => NormalizeForStorage(email) != null;
}
