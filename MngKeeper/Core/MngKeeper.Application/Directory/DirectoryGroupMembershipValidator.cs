using MngKeeper.Application.Interfaces;
using MngKeeper.Domain.Enums;

namespace MngKeeper.Application.Directory;

public static class DirectoryGroupMembershipValidator
{
  /// <summary>
  /// Yerel kullanıcıya kurumsal grup atanmasını engeller.
  /// </summary>
  public static async Task<IReadOnlyList<string>> GetDirectoryGroupIdsAsync(
    IGroupRepository groupRepository,
    string domainId,
    IEnumerable<string>? groupIds,
    CancellationToken cancellationToken = default)
  {
    if (groupIds == null || !groupIds.Any())
      return Array.Empty<string>();

    var rejected = new List<string>();
    foreach (var groupId in groupIds.Distinct(StringComparer.OrdinalIgnoreCase))
    {
      if (string.IsNullOrWhiteSpace(groupId))
        continue;

      var group = await groupRepository.GetByIdAsync(groupId, domainId);
      if (group != null && group.ProvisioningSource == UserProvisioningSource.Directory)
        rejected.Add(groupId);
    }

    return rejected;
  }
}
