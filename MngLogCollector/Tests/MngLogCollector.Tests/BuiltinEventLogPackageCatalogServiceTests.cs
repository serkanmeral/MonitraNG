using MngLogCollector.Application.Services.Policy;

namespace MngLogCollector.Tests;

public class BuiltinEventLogPackageCatalogServiceTests
{
    [Fact]
    public void GetCatalog_ReturnsDefaultsAndOptionalSecurity()
    {
        var catalog = new BuiltinEventLogPackageCatalogService().GetCatalog();

        Assert.Equal("collector", catalog.Source);
        Assert.Equal(BuiltinEventLogPackageCatalogService.CatalogVersion, catalog.Version);
        Assert.Contains(catalog.Packages, p => p.Name == "system-lifecycle");
        Assert.Contains(catalog.Packages, p => p.Name == "rdp-session");
        Assert.Contains(catalog.OptionalPackages, p => p.Name == "security-auth");
        Assert.DoesNotContain(catalog.Packages, p => p.Name == "security-auth");
    }
}
