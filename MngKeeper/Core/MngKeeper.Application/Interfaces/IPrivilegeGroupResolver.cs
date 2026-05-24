using MngKeeper.Domain.Entities;

namespace MngKeeper.Application.Interfaces;

public interface IPrivilegeGroupResolver
{
    IReadOnlyList<string> GetEffectiveAdminGroupNames(DirectoryPrivilegeSettings? settings);
    IReadOnlyList<string> GetEffectiveManagerGroupNames(DirectoryPrivilegeSettings? settings);

    bool IsAdmin(MngKeeper.Domain.Entities.Domain domain, IReadOnlyList<string>? userGroups);
    bool IsManager(MngKeeper.Domain.Entities.Domain domain, IReadOnlyList<string>? userGroups);
}
