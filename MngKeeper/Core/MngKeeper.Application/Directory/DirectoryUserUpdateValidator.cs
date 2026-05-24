using MngKeeper.Application.Common;
using MngKeeper.Application.Common.DTOs;
using MngKeeper.Application.Features.User.Commands.UpdateUser;
using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Entities;
using MngKeeper.Domain.Enums;

namespace MngKeeper.Application.Directory;

/// <summary>
/// Directory kullanıcı güncellemesinde salt okunur alan değişikliklerini doğrular.
/// </summary>
public static class DirectoryUserUpdateValidator
{
  public const string ErrorCode = "DIRECTORY_FIELD_NOT_EDITABLE";

  public static IReadOnlyList<string> GetRejectedFields(UpdateUserCommand request, User existing)
  {
    if (existing.ProvisioningSource != UserProvisioningSource.Directory)
      return Array.Empty<string>();

    var rejected = new List<string>();
    var normalizedEmail = UserEmailHelper.NormalizeForStorage(request.Email);
    var existingEmail = UserEmailHelper.NormalizeForStorage(existing.Email);

    if (!string.Equals(request.Username, existing.Username, StringComparison.OrdinalIgnoreCase))
      rejected.Add("username");

    if (!string.Equals(normalizedEmail, existingEmail, StringComparison.OrdinalIgnoreCase))
      rejected.Add("email");

    if (!string.Equals(request.FirstName, existing.FirstName, StringComparison.Ordinal))
      rejected.Add("firstName");

    if (!string.Equals(request.LastName, existing.LastName, StringComparison.Ordinal))
      rejected.Add("lastName");

    if (request.IsActive != existing.IsActive)
      rejected.Add("isActive");

    if (request.GroupIds != null)
      rejected.Add("groups");

    return rejected;
  }
}
