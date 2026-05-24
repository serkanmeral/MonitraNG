using MngKeeper.Application.Common.DTOs;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;

namespace MngKeeper.Application.Common.Mappers;

public static class UserDtoMapper
{
  public static UserDto ToDto(User user, IUserFieldPolicyService fieldPolicyService)
  {
    return new UserDto
    {
      UserId = user.Id,
      KeycloakUserId = string.IsNullOrWhiteSpace(user.KeycloakUserId) ? null : user.KeycloakUserId,
      Username = user.Username,
      Email = user.Email ?? string.Empty,
      FirstName = user.FirstName,
      LastName = user.LastName,
      Title = user.Title,
      Department = user.Department,
      Gender = user.Gender,
      PhoneNumber = user.PhoneNumber,
      PhotoUrl = user.PhotoUrl,
      IsActive = user.IsActive,
      Groups = user.Groups,
      Permissions = user.Roles,
      CreatedAt = user.CreatedAt,
      UpdatedAt = user.UpdatedAt,
      CreatedBy = user.CreatedBy,
      UpdatedBy = user.UpdatedBy,
      ProvisioningSource = user.ProvisioningSource.ToString(),
      DirectorySyncedAt = user.DirectorySyncedAt,
      Capabilities = fieldPolicyService.GetCapabilities(user),
      FieldPolicies = fieldPolicyService.GetFieldPolicies(user)
        .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase),
    };
  }
}
