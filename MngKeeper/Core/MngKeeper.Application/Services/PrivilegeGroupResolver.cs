using MngKeeper.Application.Common.Constants;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;

namespace MngKeeper.Application.Services;

public class PrivilegeGroupResolver : IPrivilegeGroupResolver
{
    public IReadOnlyList<string> GetEffectiveAdminGroupNames(DirectoryPrivilegeSettings? settings)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            SystemGroups.Admins
        };
        AddConfiguredNames(names, settings?.AdminGroupNames);
        return names.ToList();
    }

    public IReadOnlyList<string> GetEffectiveManagerGroupNames(DirectoryPrivilegeSettings? settings)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            SystemGroups.Managers
        };
        AddConfiguredNames(names, settings?.ManagerGroupNames);
        return names.ToList();
    }

    public bool IsAdmin(MngKeeper.Domain.Entities.Domain domain, IReadOnlyList<string>? userGroups)
    {
        if (userGroups == null || userGroups.Count == 0)
            return false;

        var adminNames = GetEffectiveAdminGroupNames(domain.Settings.ResolveDirectoryPrivileges());
        return userGroups.Any(g => adminNames.Contains(g, StringComparer.OrdinalIgnoreCase));
    }

    public bool IsManager(MngKeeper.Domain.Entities.Domain domain, IReadOnlyList<string>? userGroups)
    {
        if (IsAdmin(domain, userGroups))
            return true;

        if (userGroups == null || userGroups.Count == 0)
            return false;

        var managerNames = GetEffectiveManagerGroupNames(domain.Settings.ResolveDirectoryPrivileges());
        return userGroups.Any(g => managerNames.Contains(g, StringComparer.OrdinalIgnoreCase));
    }

    private static void AddConfiguredNames(HashSet<string> target, IEnumerable<string>? configured)
    {
        if (configured == null)
            return;

        foreach (var name in configured)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;
            target.Add(name.Trim());
        }
    }
}
