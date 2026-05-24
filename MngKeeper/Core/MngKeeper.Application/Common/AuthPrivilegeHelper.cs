using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;

namespace MngKeeper.Application.Common;

public static class AuthPrivilegeHelper
{
    public static async Task<bool> ResolveIsAdminAsync(
        IPrivilegeGroupResolver resolver,
        IKeycloakService keycloakService,
        MngKeeper.Domain.Entities.Domain domain,
        string username,
        IReadOnlyList<string>? userGroups)
    {
        if (userGroups != null && userGroups.Count > 0)
            return resolver.IsAdmin(domain, userGroups);

        foreach (var groupName in resolver.GetEffectiveAdminGroupNames(domain.Settings.ResolveDirectoryPrivileges()))
        {
            if (await keycloakService.IsUserInGroupAsync(domain.RealmName, username, groupName))
                return true;
        }

        return false;
    }

    public static async Task<bool> ResolveIsManagerAsync(
        IPrivilegeGroupResolver resolver,
        IKeycloakService keycloakService,
        MngKeeper.Domain.Entities.Domain domain,
        string username,
        IReadOnlyList<string>? userGroups,
        bool isAdmin)
    {
        if (isAdmin)
            return true;

        if (userGroups != null && userGroups.Count > 0)
            return resolver.IsManager(domain, userGroups);

        foreach (var groupName in resolver.GetEffectiveManagerGroupNames(domain.Settings.ResolveDirectoryPrivileges()))
        {
            if (await keycloakService.IsUserInGroupAsync(domain.RealmName, username, groupName))
                return true;
        }

        return false;
    }
}
