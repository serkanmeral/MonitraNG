namespace MngLogCollector.Application.Abstractions.Discovery;

public sealed class DirectoryLdapConfig
{
    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 389;
    public bool UseSsl { get; set; }
    public string BaseDn { get; set; } = string.Empty;
    public string BindUsername { get; set; } = string.Empty;
    public string BindPassword { get; set; } = string.Empty;
}

public sealed class DiscoveryDomainInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public DirectoryLdapConfig? DirectoryLdap { get; set; }
}

public interface IKeeperDomainDirectoryReader
{
    Task<DiscoveryDomainInfo?> GetByNameOrIdAsync(string domainNameOrId, CancellationToken ct = default);
    Task<IReadOnlyList<DiscoveryDomainInfo>> GetActiveDomainsWithLdapAsync(CancellationToken ct = default);
}

public sealed class AdComputerRecord
{
    public string? ObjectGuid { get; set; }
    public string SamAccountName { get; set; } = string.Empty;
    public string? DnsHostName { get; set; }
    public string? OperatingSystem { get; set; }
    public string? OperatingSystemVersion { get; set; }
    public string? DistinguishedName { get; set; }
    public bool? Enabled { get; set; }
}

public interface IAdComputerDirectoryClient
{
    Task<IReadOnlyList<AdComputerRecord>> SearchComputersAsync(
        DirectoryLdapConfig ldap,
        CancellationToken ct = default);
}

public interface IDiscoveryHostStore
{
    Task EnsureIndexesAsync(string databaseName, CancellationToken ct = default);

    Task UpsertManyAsync(
        string databaseName,
        IReadOnlyList<Domain.Entities.DiscoveryHost> hosts,
        CancellationToken ct = default);

    Task<(IReadOnlyList<Domain.Entities.DiscoveryHost> Items, long Total)> ListAsync(
        string databaseName,
        string domainId,
        string? query,
        string? source,
        int limit,
        int offset,
        CancellationToken ct = default);

    Task<long> CountAsync(string databaseName, string domainId, CancellationToken ct = default);

    Task<Dictionary<string, int>> CountBySourceAsync(
        string databaseName,
        string domainId,
        CancellationToken ct = default);

    Task SaveSyncStateAsync(
        string databaseName,
        Domain.Entities.DiscoverySyncState state,
        CancellationToken ct = default);

    Task<Domain.Entities.DiscoverySyncState?> GetSyncStateAsync(
        string databaseName,
        string sourceId = "ad",
        CancellationToken ct = default);
}

public interface IDiscoveryService
{
    Task<Contracts.Discovery.DiscoveryHostListResponse> ListHostsAsync(
        string domainId,
        string? query,
        string? source,
        int limit,
        int offset,
        CancellationToken ct = default);

    Task<Contracts.Discovery.DiscoverySummaryResponse> GetSummaryAsync(
        string domainId,
        CancellationToken ct = default);

    Task<Contracts.Discovery.DiscoverySyncResponse> SyncAsync(
        Contracts.Discovery.DiscoverySyncRequest request,
        CancellationToken ct = default);
}
