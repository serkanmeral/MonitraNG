using MngKeeper.Application.Common;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;
using MngKeeper.Domain.Enums;

namespace MngKeeper.Application.Directory;

public static class DirectoryUserSyncComparer
{
    public static bool ShouldSyncFromKeycloak(
        User? existing,
        KeycloakRealmUserSnapshot keycloakUser,
        IReadOnlyList<string> keycloakGroupNames)
    {
        if (existing == null)
            return true;

        if (existing.ProvisioningSource == UserProvisioningSource.Local)
            return false;

        if (!string.Equals(existing.KeycloakUserId, keycloakUser.Id, StringComparison.Ordinal))
            return true;

        if (!string.Equals(existing.Username, keycloakUser.Username, StringComparison.OrdinalIgnoreCase))
            return true;

        var storedEmail = UserEmailHelper.NormalizeForStorage(existing.Email);
        var kcEmail = UserEmailHelper.NormalizeForStorage(keycloakUser.Email);
        if (!string.Equals(storedEmail, kcEmail, StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.Equals(existing.FirstName, keycloakUser.FirstName ?? string.Empty, StringComparison.Ordinal))
            return true;

        if (!string.Equals(existing.LastName, keycloakUser.LastName ?? string.Empty, StringComparison.Ordinal))
            return true;

        if (existing.IsActive != keycloakUser.Enabled)
            return true;

        if (DirectoryProfileDiffers(existing.Title, keycloakUser.Title))
            return true;

        if (DirectoryProfileDiffers(existing.Department, keycloakUser.Department))
            return true;

        return !GroupSetsEqual(existing.Groups, keycloakGroupNames);
    }

    /// <summary>Boş dizin değeri fark sayılmaz; dolu değer mevcuttan farklıysa senkron gerekir.</summary>
    public static bool DirectoryProfileDiffers(string? current, string? incoming)
    {
        if (string.IsNullOrWhiteSpace(incoming))
            return false;
        return !string.Equals(current?.Trim(), incoming.Trim(), StringComparison.Ordinal);
    }

    private static bool GroupSetsEqual(IReadOnlyList<string>? mongoGroups, IReadOnlyList<string> keycloakGroups)
    {
        var a = (mongoGroups ?? Array.Empty<string>())
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Select(g => g.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var b = keycloakGroups
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Select(g => g.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return a.SetEquals(b);
    }
}
