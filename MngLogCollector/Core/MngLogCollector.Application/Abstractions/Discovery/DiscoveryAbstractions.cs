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

    /// <summary>Upsert scan results (by ObjectGuid = scan:ip or merged AD guid).</summary>
    Task UpsertScanHostsAsync(
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

    Task<IReadOnlyList<Domain.Entities.DiscoveryHost>> ListAllAsync(
        string databaseName,
        string domainId,
        CancellationToken ct = default);

    Task<long> CountAsync(string databaseName, string domainId, CancellationToken ct = default);

    Task<Dictionary<string, int>> CountBySourceAsync(
        string databaseName,
        string domainId,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes discovery hosts for a domain. When <paramref name="source"/> is set,
    /// only hosts that include that source tag are removed.
    /// </summary>
    Task<long> DeleteAsync(
        string databaseName,
        string domainId,
        string? source = null,
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

public interface IDiscoveryScanJobStore
{
    Task EnsureIndexesAsync(string databaseName, CancellationToken ct = default);

    Task InsertAsync(string databaseName, Domain.Entities.DiscoveryScanJob job, CancellationToken ct = default);

    Task<Domain.Entities.DiscoveryScanJob?> GetAsync(
        string databaseName,
        string runId,
        CancellationToken ct = default);

    Task UpdateAsync(string databaseName, Domain.Entities.DiscoveryScanJob job, CancellationToken ct = default);

    Task<Domain.Entities.DiscoveryScanJob?> FindActiveAsync(
        string databaseName,
        string domainId,
        CancellationToken ct = default);
}

public interface IDiscoveryScanQueue
{
    ValueTask EnqueueAsync(string databaseName, string runId, CancellationToken ct = default);
    IAsyncEnumerable<(string DatabaseName, string RunId)> ReadAllAsync(CancellationToken ct);
}

public interface IDiscoveryPrefixStore
{
    Task EnsureIndexesAsync(string databaseName, CancellationToken ct = default);

    Task<Domain.Entities.DiscoveryPrefixTableDocument?> GetAsync(
        string databaseName,
        string domainId,
        CancellationToken ct = default);

    Task UpsertAsync(
        string databaseName,
        Domain.Entities.DiscoveryPrefixTableDocument document,
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

    Task<Contracts.Discovery.DiscoveryScanStartResponse> StartScanAsync(
        Contracts.Discovery.DiscoveryScanStartRequest request,
        CancellationToken ct = default);

    Task<Contracts.Discovery.DiscoveryScanStatusResponse?> GetScanAsync(
        string domainId,
        string runId,
        CancellationToken ct = default);

    Task<Contracts.Discovery.DiscoveryScanStatusResponse?> CancelScanAsync(
        string domainId,
        string runId,
        CancellationToken ct = default);

    Task<Contracts.Discovery.DiscoveryClearResponse> ClearHostsAsync(
        Contracts.Discovery.DiscoveryClearRequest request,
        CancellationToken ct = default);

    Task<Contracts.Discovery.DiscoveryPrefixesResponse> GetPrefixesAsync(
        string domainId,
        CancellationToken ct = default);

    Task<Contracts.Discovery.DiscoveryPrefixesResponse> PutPrefixesAsync(
        string domainId,
        Contracts.Discovery.DiscoveryPrefixesPutRequest request,
        CancellationToken ct = default);
}
