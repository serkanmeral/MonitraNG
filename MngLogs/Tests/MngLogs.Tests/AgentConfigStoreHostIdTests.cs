using Microsoft.Extensions.Options;
using MngLogs.Agent.Configuration;

namespace MngLogs.Tests;

public class AgentConfigStoreHostIdTests
{
    [Fact]
    public void EmptyHostId_DefaultsToMachineName_AndPersists()
    {
        var dir = Path.Combine(Path.GetTempPath(), "MngLogs-HostId-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var settings = new MngLogsAgentSettings
            {
                System = new SystemConfig { DataDirectory = dir, HostId = "" }
            };
            var store = new AgentConfigStore(Options.Create(settings));

            Assert.Equal(Environment.MachineName, store.ResolveHostId());
            Assert.Equal(Environment.MachineName, store.Current.System.HostId);

            var path = Path.Combine(dir, "system.json");
            Assert.True(File.Exists(path));
            var json = File.ReadAllText(path);
            Assert.Contains(Environment.MachineName, json, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }
}
