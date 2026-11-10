using MngKeeper.Application.Directory;
using MngKeeper.Domain.Enums;
using Xunit;

namespace MngKeeper.Application.Tests.Directory;

public class DirectoryGroupPolicyTests
{
  [Theory]
  [InlineData("admins", true)]
  [InlineData("Managers", true)]
  [InlineData("IT-Team", false)]
  [InlineData("Domain Users", false)]
  public void IsProtectedLocalGroupName_respects_system_groups(string name, bool expected)
  {
    Assert.Equal(expected, DirectoryGroupPolicy.IsProtectedLocalGroupName(name));
  }

  [Theory]
  [InlineData(UserProvisioningSource.Local, true)]
  [InlineData(UserProvisioningSource.Directory, false)]
  public void StaysLocalDuringSync_keeps_application_groups(UserProvisioningSource source, bool expected)
  {
    Assert.Equal(expected, DirectoryGroupPolicy.StaysLocalDuringSync(source));
  }
}
