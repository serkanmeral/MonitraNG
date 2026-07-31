using Microsoft.Extensions.Logging.Abstractions;
using MngLogCollector.Application.Abstractions.Discovery;
using MngLogCollector.Application.Contracts.Discovery;
using MngLogCollector.Application.Services.Discovery;
using MngLogCollector.Domain.Entities;

namespace MngLogCollector.Tests;

public class DiscoveryServiceTests
{
    [Fact]
    public async Task SyncAsync_RejectsNonAdSource()
    {
        var sut = CreateSut(new FakeDomains(), new FakeAd(), new FakeStore());
        var result = await sut.SyncAsync(new DiscoverySyncRequest { Source = "dhcp" });
        Assert.Equal("error", result.Status);
        Assert.Contains("ad", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ListHostsAsync_MapsHostnameFromDns()
    {
        var store = new FakeStore();
        store.Hosts.Add(new DiscoveryHost
        {
            Id = ObjectIdLike(),
            DomainId = "odak",
            SamAccountName = "PC01$",
            DnsHostName = "pc01.odak.local",
            DisplayName = "pc01.odak.local",
            OperatingSystem = "Windows 11",
            Sources = ["ad"],
            LastSeenFromAd = DateTime.UtcNow
        });

        var sut = CreateSut(new FakeDomains(), new FakeAd(), store);
        var list = await sut.ListHostsAsync("odak", null, null, 50, 0);
        Assert.Equal(1, list.Total);
        Assert.Equal("pc01.odak.local", list.Items[0].Hostname);
        Assert.Equal("Windows 11", list.Items[0].OsHint);
    }

    private static DiscoveryService CreateSut(
        IKeeperDomainDirectoryReader domains,
        IAdComputerDirectoryClient ad,
        IDiscoveryHostStore store) =>
        new(domains, ad, store, NullLogger<DiscoveryService>.Instance);

    private static string ObjectIdLike() => "507f1f77bcf86cd799439011";

    private sealed class FakeDomains : IKeeperDomainDirectoryReader
    {
        public Task<DiscoveryDomainInfo?> GetByNameOrIdAsync(string domainNameOrId, CancellationToken ct = default) =>
            Task.FromResult<DiscoveryDomainInfo?>(new DiscoveryDomainInfo
            {
                Id = "1",
                Name = "odak",
                DatabaseName = "mng_odak",
                DirectoryLdap = new DirectoryLdapConfig
                {
                    Enabled = true,
                    Host = "dc",
                    BaseDn = "DC=odak,DC=local",
                    BindUsername = "u",
                    BindPassword = "p"
                }
            });

        public Task<IReadOnlyList<DiscoveryDomainInfo>> GetActiveDomainsWithLdapAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DiscoveryDomainInfo>>([GetByNameOrIdAsync("odak", ct).Result!]);
    }

    private sealed class FakeAd : IAdComputerDirectoryClient
    {
        public Task<IReadOnlyList<AdComputerRecord>> SearchComputersAsync(
            DirectoryLdapConfig ldap,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AdComputerRecord>>([]);
    }

    private sealed class FakeStore : IDiscoveryHostStore
    {
        public List<DiscoveryHost> Hosts { get; } = [];
        public DiscoverySyncState? State { get; private set; }

        public Task EnsureIndexesAsync(string databaseName, CancellationToken ct = default) => Task.CompletedTask;

        public Task UpsertManyAsync(string databaseName, IReadOnlyList<DiscoveryHost> hosts, CancellationToken ct = default)
        {
            Hosts.AddRange(hosts);
            return Task.CompletedTask;
        }

        public Task<(IReadOnlyList<DiscoveryHost> Items, long Total)> ListAsync(
            string databaseName,
            string domainId,
            string? query,
            string? source,
            int limit,
            int offset,
            CancellationToken ct = default)
        {
            var items = Hosts.Where(h => h.DomainId == domainId).Skip(offset).Take(limit).ToList();
            return Task.FromResult(((IReadOnlyList<DiscoveryHost>)items, (long)Hosts.Count));
        }

        public Task<long> CountAsync(string databaseName, string domainId, CancellationToken ct = default) =>
            Task.FromResult((long)Hosts.Count(h => h.DomainId == domainId));

        public Task<Dictionary<string, int>> CountBySourceAsync(
            string databaseName,
            string domainId,
            CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<string, int> { ["ad"] = Hosts.Count });

        public Task SaveSyncStateAsync(string databaseName, DiscoverySyncState state, CancellationToken ct = default)
        {
            State = state;
            return Task.CompletedTask;
        }

        public Task<DiscoverySyncState?> GetSyncStateAsync(
            string databaseName,
            string sourceId = "ad",
            CancellationToken ct = default) =>
            Task.FromResult(State);
    }
}
