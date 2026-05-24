using MngKeeper.Domain.Entities;
using MngKeeper.Domain.Enums;

namespace MngKeeper.Application.Directory;

/// <summary>Kurumsal (Directory) gruplar — UI/API üzerinden değiştirilemez.</summary>
public static class DirectoryGroupGuard
{
  public const string ErrorCode = "DIRECTORY_GROUP_NOT_MUTABLE";

  public const string MembershipErrorCode = "DIRECTORY_GROUP_MEMBERSHIP_NOT_MUTABLE";

  public static bool IsDirectoryGroup(Group group) =>
    group.ProvisioningSource == UserProvisioningSource.Directory;

  public static bool CanMutateGroup(Group group) => !IsDirectoryGroup(group);

  public static bool CanManageMembership(Group group) => !IsDirectoryGroup(group);
}
