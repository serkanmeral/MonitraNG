using MngKeeper.Application.Directory;
using MngKeeper.Application.Features.User.Commands.UpdateUser;
using MngKeeper.Application.Interfaces;
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

  [Fact]
  public void Directory_user_can_change_password_only_when_ad_write_is_enabled()
  {
    var user = new User { ProvisioningSource = UserProvisioningSource.Directory };
    var closed = _service.GetCapabilities(user, directoryPasswordChangeEnabled: false);
    var open = _service.GetCapabilities(user, directoryPasswordChangeEnabled: true);

    Assert.False(closed.CanChangePassword);
    Assert.True(open.CanChangePassword);
    Assert.False(open.CanManageGroups);
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

public class DirectoryProfileSyncTests
{
  [Fact]
  public void Copies_department_and_title_when_directory_sends_them()
  {
    var user = new User { Title = "Eski", Department = "Eski birim" };
    DirectoryUserFieldSets.ApplyDirectoryProfile(user, new KeycloakRealmUserSnapshot
    {
      Title = "Projeler Koordinatörü",
      Department = "Yönetim"
    });

    Assert.Equal("Projeler Koordinatörü", user.Title);
    Assert.Equal("Yönetim", user.Department);
  }

  [Fact]
  public void Empty_directory_profile_keeps_the_current_values()
  {
    var user = new User { Title = "Eski", Department = "Eski birim" };
    DirectoryUserFieldSets.ApplyDirectoryProfile(user, new KeycloakRealmUserSnapshot());

    Assert.Equal("Eski", user.Title);
    Assert.Equal("Eski birim", user.Department);
    Assert.False(DirectoryUserSyncComparer.DirectoryProfileDiffers("Eski birim", null));
    Assert.False(DirectoryUserSyncComparer.DirectoryProfileDiffers("Eski birim", "  "));
  }

  [Fact]
  public void Different_directory_department_needs_sync()
  {
    Assert.True(DirectoryUserSyncComparer.DirectoryProfileDiffers(null, "Yönetim"));
    Assert.False(DirectoryUserSyncComparer.DirectoryProfileDiffers(" Yönetim ", "Yönetim"));
  }
}
