using MngKeeper.Application.Directory;
using MngKeeper.Application.Features.User.Commands.UpdateUser;
using MngKeeper.Application.Services;
using MngKeeper.Domain.Entities;
using MngKeeper.Domain.Enums;
using Xunit;

namespace MngKeeper.Application.Tests.Directory;

public class UserFieldPolicyServiceTests
{
  private readonly UserFieldPolicyService _service = new();

  [Fact]
  public void Directory_user_has_readonly_identity_fields()
  {
    var user = new User { ProvisioningSource = UserProvisioningSource.Directory };
    var policies = _service.GetFieldPolicies(user);

    Assert.False(policies["email"].Editable);
    Assert.Equal("directory", policies["email"].Source);
    Assert.True(policies["title"].Editable);
    Assert.Equal("app", policies["title"].Source);
  }

  [Fact]
  public void Local_user_capabilities_allow_management()
  {
    var user = new User { ProvisioningSource = UserProvisioningSource.Local };
    var caps = _service.GetCapabilities(user);

    Assert.True(caps.CanManageGroups);
    Assert.True(caps.CanChangePassword);
  }

  [Fact]
  public void Directory_user_capabilities_are_restricted()
  {
    var user = new User { ProvisioningSource = UserProvisioningSource.Directory };
    var caps = _service.GetCapabilities(user);

    Assert.False(caps.CanManageGroups);
    Assert.False(caps.CanChangePassword);
  }
}

public class DirectoryUserUpdateValidatorTests
{
  [Fact]
  public void Rejects_email_change_for_directory_user()
  {
    var existing = new User
    {
      ProvisioningSource = UserProvisioningSource.Directory,
      Username = "jdoe",
      Email = "a@x.com",
      FirstName = "J",
      LastName = "D",
      IsActive = true,
    };
    var request = new UpdateUserCommand
    {
      Username = "jdoe",
      Email = "b@x.com",
      FirstName = "J",
      LastName = "D",
      IsActive = true,
    };

    var rejected = DirectoryUserUpdateValidator.GetRejectedFields(request, existing);

    Assert.Contains("email", rejected);
  }

  [Fact]
  public void Allows_app_only_update_payload_for_directory_user()
  {
    var existing = new User
    {
      ProvisioningSource = UserProvisioningSource.Directory,
      Username = "jdoe",
      Email = "a@x.com",
      FirstName = "J",
      LastName = "D",
      IsActive = true,
      Title = "Old",
    };
    var request = new UpdateUserCommand
    {
      Username = "jdoe",
      Email = "a@x.com",
      FirstName = "J",
      LastName = "D",
      IsActive = true,
      Title = "New",
    };

    var rejected = DirectoryUserUpdateValidator.GetRejectedFields(request, existing);

    Assert.Empty(rejected);
  }
}
