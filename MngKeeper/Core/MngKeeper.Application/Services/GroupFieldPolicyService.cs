using MngKeeper.Application.Common.DTOs;
using MngKeeper.Application.Directory;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;

namespace MngKeeper.Application.Services;

public class GroupFieldPolicyService : IGroupFieldPolicyService
{
  public GroupCapabilitiesDto GetCapabilities(Group group)
  {
    if (DirectoryGroupGuard.IsDirectoryGroup(group))
    {
      return new GroupCapabilitiesDto
      {
        CanEdit = false,
        CanDelete = false,
        CanManageMembers = false,
        CanChangeApplicationScope = true,
      };
    }

    return new GroupCapabilitiesDto
    {
      CanEdit = true,
      CanDelete = true,
      CanManageMembers = true,
      CanChangeApplicationScope = true,
    };
  }
}
