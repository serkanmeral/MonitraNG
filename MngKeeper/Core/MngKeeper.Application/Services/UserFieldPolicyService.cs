using MngKeeper.Application.Common.DTOs;
using MngKeeper.Application.Directory;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;
using MngKeeper.Domain.Enums;

namespace MngKeeper.Application.Services;

public class UserFieldPolicyService : IUserFieldPolicyService
{
  private static readonly string[] PolicyFieldNames =
  {
    "username", "email", "firstName", "lastName", "groups", "isActive",
    "photoUrl", "gender", "title", "department", "phoneNumber",
  };

  public UserCapabilitiesDto GetCapabilities(User user)
  {
    if (user.ProvisioningSource == UserProvisioningSource.Directory)
    {
      return new UserCapabilitiesDto
      {
        CanChangePassword = false,
        CanManageGroups = false,
        CanDeactivate = false,
        CanDelete = false,
      };
    }

    return new UserCapabilitiesDto
    {
      CanChangePassword = true,
      CanManageGroups = true,
      CanDeactivate = true,
      CanDelete = true,
    };
  }

  public IReadOnlyDictionary<string, UserFieldPolicyItemDto> GetFieldPolicies(User user)
  {
    var isDirectory = user.ProvisioningSource == UserProvisioningSource.Directory;
    var result = new Dictionary<string, UserFieldPolicyItemDto>(StringComparer.OrdinalIgnoreCase);

    foreach (var field in PolicyFieldNames)
    {
      if (!isDirectory)
      {
        result[field] = new UserFieldPolicyItemDto { Editable = true, Source = "app" };
        continue;
      }

      if (DirectoryUserFieldSets.SyncFromKeycloak.Contains(field))
      {
        result[field] = new UserFieldPolicyItemDto { Editable = false, Source = "directory" };
      }
      else if (DirectoryUserFieldSets.EditableByApplication.Contains(field))
      {
        result[field] = new UserFieldPolicyItemDto { Editable = true, Source = "app" };
      }
      else
      {
        result[field] = new UserFieldPolicyItemDto { Editable = false, Source = "system" };
      }
    }

    return result;
  }
}
