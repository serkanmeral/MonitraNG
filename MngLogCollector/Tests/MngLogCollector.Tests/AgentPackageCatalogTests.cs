using Microsoft.Extensions.Options;
using MngLogCollector.Application.Configuration;
using MngLogCollector.Application.Services.AgentPackages;

namespace MngLogCollector.Tests;

public class AgentPackageCatalogTests
{
    [Fact]
    public void Missing_Directory_Returns_Empty()
    {
        var catalog = Create(Path.Combine(Path.GetTempPath(), "mnglogs-packages-missing-" + Guid.NewGuid().ToString("N")));
        var result = catalog.GetCatalog("http://192.168.20.20:5091");
        Assert.Empty(result.Packages);
        Assert.Equal("http://192.168.20.20:5091", result.CollectorBaseUrl);
    }

    [Fact]
    public void Auto_Detects_Windows_And_Linux_Files()
    {
        var dir = CreateTempDir();
        try
        {
            File.WriteAllBytes(Path.Combine(dir, "MngLogs.Agent-1.2.3.msi"), [1, 2, 3, 4]);
            File.WriteAllBytes(Path.Combine(dir, "mnglogs-agent-linux-x64-0.3.1.tar.gz"), [5, 6, 7]);

            var catalog = Create(dir, publicBaseUrl: "http://192.168.20.20:5091");
            var result = catalog.GetCatalog(null);

            Assert.Equal(2, result.Packages.Count);
            var win = result.Packages.Single(p => p.Id == "windows");
            Assert.Equal("1.2.3", win.Version);
            Assert.Equal("http://192.168.20.20:5091/api/v1/agent/packages/windows", win.DownloadUrl);
            Assert.Equal(4, win.SizeBytes);
            Assert.False(string.IsNullOrWhiteSpace(win.Sha256));

            var linux = result.Packages.Single(p => p.Id == "linux");
            Assert.Equal("0.3.1", linux.Version);
            var file = catalog.GetFile("linux");
            Assert.NotNull(file);
            Assert.True(File.Exists(file!.AbsolutePath));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Manifest_Wins_And_Rejects_Path_Traversal()
    {
        var dir = CreateTempDir();
        var outside = Path.Combine(Path.GetTempPath(), "mnglogs-outside-" + Guid.NewGuid().ToString("N") + ".msi");
        try
        {
            File.WriteAllBytes(Path.Combine(dir, "windows.msi"), [9]);
            File.WriteAllBytes(outside, [8]);
            File.WriteAllText(Path.Combine(dir, "manifest.json"), """
                {
                  "packages": [
                    { "id": "windows", "fileName": "windows.msi", "displayFileName": "MngLogs.Agent-9.0.0.msi", "version": "9.0.0", "sha256": "abc" },
                    { "id": "linux", "fileName": "../secret.tar.gz" }
                  ]
                }
                """);

            var catalog = Create(dir);
            var result = catalog.GetCatalog("http://collector:5091");
            Assert.Single(result.Packages);
            Assert.Equal("windows", result.Packages[0].Id);
            Assert.Equal("MngLogs.Agent-9.0.0.msi", result.Packages[0].FileName);
            Assert.Equal("abc", result.Packages[0].Sha256);
            Assert.Null(catalog.GetFile("linux"));
            Assert.Null(catalog.GetFile("../windows"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
            if (File.Exists(outside)) File.Delete(outside);
        }
    }

    private static AgentPackageCatalog Create(string directory, string publicBaseUrl = "")
    {
        var settings = new MngLogCollectorSettings
        {
            AgentPackages = new AgentPackagesSettings
            {
                Directory = directory,
                PublicBaseUrl = publicBaseUrl
            }
        };
        return new AgentPackageCatalog(Options.Create(settings));
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mnglogs-packages-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
