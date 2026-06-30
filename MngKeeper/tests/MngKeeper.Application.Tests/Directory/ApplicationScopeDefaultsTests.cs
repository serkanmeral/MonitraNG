using MngKeeper.Application.Directory;
using MngKeeper.Domain.Enums;
using MongoDB.Bson;
using Xunit;

namespace MngKeeper.Application.Tests.Directory;

public class ApplicationScopeDefaultsTests
{
  [Theory]
  [InlineData(UserProvisioningSource.Local, true)]
  [InlineData(UserProvisioningSource.Directory, false)]
  public void DefaultForSource(UserProvisioningSource source, bool expected)
  {
    Assert.Equal(expected, ApplicationScopeDefaults.DefaultForSource(source));
  }

  [Fact]
  public void ResolveFromDocument_uses_stored_value_when_present()
  {
    var doc = new BsonDocument
    {
      ["provisioningSource"] = (int)UserProvisioningSource.Directory,
      ["includeInApplication"] = true,
    };
    Assert.True(ApplicationScopeDefaults.ResolveFromDocument(doc));
  }

  [Fact]
  public void ResolveFromDocument_legacy_directory_without_field_is_hidden()
  {
    var doc = new BsonDocument { ["provisioningSource"] = (int)UserProvisioningSource.Directory };
    Assert.False(ApplicationScopeDefaults.ResolveFromDocument(doc));
  }

  [Fact]
  public void CanAuthenticate_requires_active_and_in_scope()
  {
    Assert.True(ApplicationScopeDefaults.CanAuthenticate(true, true));
    Assert.False(ApplicationScopeDefaults.CanAuthenticate(false, true));
    Assert.False(ApplicationScopeDefaults.CanAuthenticate(true, false));
  }
}
