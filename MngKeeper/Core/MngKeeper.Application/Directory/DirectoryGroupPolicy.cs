using MngKeeper.Application.Common.Constants;
using MngKeeper.Domain.Enums;

namespace MngKeeper.Application.Directory;

/// <summary>
/// Uygulamada Local kaydı olan grup sync sırasında Directory yapılmaz.
/// Keycloak'ta ilk kez görülen grup LDAP kaynağı sayılır ve Directory olur.
/// admins, managers, users ve guests isimleri sistem grubudur.
/// </summary>
public static class DirectoryGroupPolicy
{
  public static bool IsProtectedLocalGroupName(string? name)
  {
    if (string.IsNullOrWhiteSpace(name))
      return false;

    return SystemGroups.All.Any(
      g => string.Equals(g, name.Trim(), StringComparison.OrdinalIgnoreCase));
  }

  /// <summary>
  /// Mevcut kayıt Local ise üyelik uygulama üzerinden yönetilmeye devam eder.
  /// </summary>
  public static bool StaysLocalDuringSync(UserProvisioningSource existingSource) =>
    existingSource == UserProvisioningSource.Local;
}
