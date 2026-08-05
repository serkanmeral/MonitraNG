using System.Text.Json;
using Microsoft.Extensions.Configuration;
using MngAlarm.Application.Contracts;
using MngAlarm.Infrastructure.Evaluation;
using MngAlarm.Infrastructure.Services;

namespace MngAlarm.Tests.Evaluation;

public sealed class ScenarioTemplatePackageTests
{
    [Fact]
    public void Product_v2_package_contains_valid_U1_through_U10()
    {
        var path = Path.GetFullPath(
            "../../../../../../tests/fixtures/siem/scenario_templates/packages/siem-product-v2/manifest.json",
            AppContext.BaseDirectory);
        var package = JsonSerializer.Deserialize<ImportScenarioPackageRequest>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(package);
        Assert.Equal("siem-product-v2", package!.PackageId);
        Assert.Equal(10, package.Templates.Count);
        Assert.Equal(
            Enumerable.Range(1, 10).Select(x => $"U{x}"),
            package.Templates.Select(x => x.TemplateId));
        Assert.All(package.Templates, template =>
            Assert.True(
                ScenarioCompiler.Validate(template.Definition, false).IsValid,
                template.TemplateId));
    }

    [Fact]
    public void Package_import_is_default_deny_and_requires_exact_server_key()
    {
        var disabled = new ScenarioPackageImportAuthorizer(new ConfigurationBuilder().Build());
        var enabled = new ScenarioPackageImportAuthorizer(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ScenarioStudio:PackageImportKey"] = "server-secret"
                })
                .Build());

        Assert.False(disabled.IsAuthorized("anything"));
        Assert.False(enabled.IsAuthorized("wrong"));
        Assert.True(enabled.IsAuthorized("server-secret"));
    }
}
