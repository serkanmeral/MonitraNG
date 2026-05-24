using MngKeeper.Application.Directory;
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
}
