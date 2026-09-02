using MngLogs.Agent.Configuration;
using MngLogs.Agent.Contracts;
using MngLogs.Agent.Dlp;
using MngLogs.Agent.EventLog;
using MngLogs.Agent.Transport;
using Microsoft.Extensions.Options;

namespace MngLogs.Tests;

public class EventLogPackageCatalogStoreTests
{
    [Fact]
    public async Task RefreshAsync_UsesCollectorCatalog_WhenAvailable()
    {
        var dir = Path.Combine(Path.GetTempPath(), "MngLogs-Catalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var collector = new FakeCollectorClient
            {
                Catalog = new EventLogPackageCatalogResponse
                {
                    Source = "collector",
                    Version = "test-1",
                    Packages =
                    [
                        new EventLogPackageCatalogItem
                        {
                            Name = "from-server",
                            Channel = "System",
                            EventIds = [6005]
                        }
                    ],
                    OptionalPackages =
                    [
                        new EventLogPackageCatalogItem
                        {
                            Name = "opt",
                            Channel = "Security",
                            EventIds = [4624]
                        }
                    ]
                }
            };

            var store = new EventLogPackageCatalogStore(CreateConfig(dir), collector);
            await store.RefreshAsync();

            Assert.Equal("collector", store.Source);
            Assert.Equal("test-1", store.Version);
            Assert.Single(store.ServerPackages);
            Assert.Equal("from-server", store.ServerPackages[0].Name);
            Assert.Single(store.OptionalPackages);
            Assert.Equal("opt", store.OptionalPackages[0].Name);
            Assert.True(File.Exists(Path.Combine(dir, "server-packages.json")));
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task RefreshAsync_FallsBackToBuiltin_WhenCollectorFails()
    {
        var dir = Path.Combine(Path.GetTempPath(), "MngLogs-Catalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var store = new EventLogPackageCatalogStore(CreateConfig(dir), new FakeCollectorClient());
            await store.RefreshAsync();

            Assert.Equal("builtin", store.Source);
            Assert.Contains(store.ServerPackages, p => p.Name == "system-lifecycle");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task RefreshAsync_AcceptsAllChannelPackages()
    {
        var dir = Path.Combine(Path.GetTempPath(), "MngLogs-Catalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var collector = new FakeCollectorClient
            {
                Catalog = new EventLogPackageCatalogResponse
                {
                    Source = "collector",
                    Version = "all-1",
                    Packages =
                    [
                        new EventLogPackageCatalogItem
                        {
                            Name = "system-all",
                            Channel = "System",
                            SelectionMode = "all",
                            EventIds = [],
                            ExcludedEventIds = [7036]
                        }
                    ]
                }
            };

            var store = new EventLogPackageCatalogStore(CreateConfig(dir), collector);
            var result = await store.RefreshAsync();

            Assert.True(result.Ok);
            Assert.Equal("collector", store.Source);
            Assert.Single(store.ServerPackages);
            Assert.Equal("all", store.ServerPackages[0].SelectionMode);
            Assert.Equal(new[] { 7036 }, store.ServerPackages[0].ExcludedEventIds);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task RefreshAsync_NotModified_KeepsCatalog()
    {
        var dir = Path.Combine(Path.GetTempPath(), "MngLogs-Catalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var collector = new FakeCollectorClient
            {
                Catalog = new EventLogPackageCatalogResponse
                {
                    Source = "collector",
                    Version = "v1",
                    Packages =
                    [
                        new EventLogPackageCatalogItem { Name = "a", Channel = "System", EventIds = [1] }
                    ]
                }
            };
            var store = new EventLogPackageCatalogStore(CreateConfig(dir), collector);
            await store.RefreshAsync();
            Assert.Equal("v1", store.Version);

            collector.ReturnNotModified = true;
            await store.RefreshAsync();
            Assert.Equal("collector", store.Source);
            Assert.Equal("v1", store.Version);
            Assert.Single(store.ServerPackages);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { /* ignore */ }
        }
    }

    private static IAgentConfigStore CreateConfig(string dataDir)
    {
        var settings = new MngLogsAgentSettings
        {
            System = new SystemConfig { DataDirectory = dataDir }
        };
        return new AgentConfigStore(Options.Create(settings));
    }

    private sealed class FakeCollectorClient : ICollectorClient
    {
        public EventLogPackageCatalogResponse? Catalog { get; set; }
        public bool ReturnNotModified { get; set; }

        public Task<bool> HealthAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<IngestBatchResponse?> SendBatchAsync(
            IngestBatchRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IngestBatchResponse?>(null);

        public Task<EventLogPackageCatalogPullResult> GetEventLogPackageCatalogAsync(
            string? ifNoneMatchVersion = null,
            CancellationToken cancellationToken = default)
        {
            if (ReturnNotModified)
                return Task.FromResult(EventLogPackageCatalogPullResult.Unchanged());
            if (Catalog is null)
                return Task.FromResult(EventLogPackageCatalogPullResult.Failed());
            return Task.FromResult(EventLogPackageCatalogPullResult.Ok(Catalog));
        }

        public Task<DlpPolicyPullResult> GetDlpPolicyAsync(
            string? ifNoneMatchVersion = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(DlpPolicyPullResult.Failed());
    }
}
