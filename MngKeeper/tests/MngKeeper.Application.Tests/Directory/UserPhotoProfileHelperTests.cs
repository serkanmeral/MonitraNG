using MngKeeper.Application.Directory;
using MngKeeper.Domain.Entities;
using MngKeeper.Domain.Enums;
using Xunit;

namespace MngKeeper.Application.Tests.Directory;

public class UserPhotoProfileHelperTests
{
  [Fact]
  public void ApplyPhotoUrlFromRequest_unchanged_preserves_directory_source()
  {
    var user = new User
    {
      PhotoUrl = "/keeper/api/user/abc/photo",
      PhotoSource = UserPhotoSource.Directory,
      DirectoryPhotoHash = "HASH",
    };

    UserPhotoProfileHelper.ApplyPhotoUrlFromRequest(user, "/keeper/api/user/abc/photo");

    Assert.Equal(UserPhotoSource.Directory, user.PhotoSource);
    Assert.Equal("HASH", user.DirectoryPhotoHash);
  }

  [Fact]
  public void ApplyPhotoUrlFromRequest_new_url_sets_manual()
  {
    var user = new User
    {
      PhotoUrl = "/keeper/api/user/abc/photo",
      PhotoSource = UserPhotoSource.Directory,
      DirectoryPhotoHash = "HASH",
    };

    UserPhotoProfileHelper.ApplyPhotoUrlFromRequest(user, "/keeper/api/user/abc/photo?v=2");

    Assert.Equal(UserPhotoSource.Manual, user.PhotoSource);
    Assert.Null(user.DirectoryPhotoHash);
  }

  [Fact]
  public void ClearPhoto_resets_source_and_hash()
  {
    var user = new User
    {
      PhotoUrl = "/keeper/api/user/abc/photo",
      PhotoSource = UserPhotoSource.Manual,
    };

    UserPhotoProfileHelper.ClearPhoto(user);

    Assert.Null(user.PhotoUrl);
    Assert.Equal(UserPhotoSource.None, user.PhotoSource);
    Assert.Null(user.DirectoryPhotoHash);
  }
}
