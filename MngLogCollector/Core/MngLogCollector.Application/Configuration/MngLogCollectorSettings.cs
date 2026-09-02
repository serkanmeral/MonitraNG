namespace MngLogCollector.Application.Configuration;

public sealed class MngLogCollectorSettings
{
    public const string SectionName = "MngLogCollectorSettings";

    public ServerSettings Server { get; set; } = new();
    public OpenSearchSettings OpenSearch { get; set; } = new();
    public IngestSettings Ingest { get; set; } = new();
    public MongoDbSettings MongoDB { get; set; } = new();
    public DiscoverySettings Discovery { get; set; } = new();
    public AgentPackagesSettings AgentPackages { get; set; } = new();
    public RabbitMqSettings RabbitMq { get; set; } = new();
    public ObservationPublishSettings ObservationPublish { get; set; } = new();
}

/// <summary>IT-facing Windows/Linux agent installers served from this collector.</summary>
public sealed class AgentPackagesSettings
{
    /// <summary>Host folder mounted into the container (MSI + tar.gz + optional manifest.json).</summary>
    public string Directory { get; set; } = "/var/lib/mnglogcollector/agent-packages";

    /// <summary>
    /// Public collector base for install commands (LAN URL agents will use).
    /// Empty = derive from the incoming request.
    /// </summary>
    public string PublicBaseUrl { get; set; } = string.Empty;
}

/// <summary>Site/subnet prefix table for honest discovery grouping (IPAM-style LPM).</summary>
public sealed class DiscoverySettings
{
    public List<DiscoveryPrefixEntry> Prefixes { get; set; } = [];
}

public sealed class DiscoveryPrefixEntry
{
    /// <summary>IPv4 CIDR, e.g. 192.168.20.0/24</summary>
    public string Cidr { get; set; } = string.Empty;

    /// <summary>Human label, e.g. Odak ofis</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Optional operator-mapped VLAN name (not inferred from IP).</summary>
    public string? VlanName { get; set; }
}

public sealed class ServerSettings
{
    public string Host { get; set; } = "0.0.0.0";
    public int Port { get; set; } = 5091;
    public string Scheme { get; set; } = "http";
}

public sealed class OpenSearchSettings
{
    /// <summary>Base URL, e.g. http://opensearch:9200</summary>
    public string Url { get; set; } = "http://opensearch:9200";

    /// <summary>When false, ingest accepts batches but does not write to OpenSearch.</summary>
    public bool WriteEnabled { get; set; } = true;
}

public sealed class IngestSettings
{
    /// <summary>
    /// Shared agent API key. Empty = auth disabled (dev only).
    /// Agents send header: X-MngLogs-ApiKey
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    public int MaxEventsPerBatch { get; set; } = 500;
}

public sealed class MongoDbSettings
{
    /// <summary>Full connection string. When empty, Host/Port/Username/Password are used.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 27017;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    /// <summary>MngKeeper database that holds the domains collection.</summary>
    public string KeeperDatabaseName { get; set; } = "mngkeeper";

    /// <summary>
    /// Tenant DB for editable Event Log package catalog (Odak: mng_odak).
    /// </summary>
    public string EventLogCatalogDatabaseName { get; set; } = "mng_odak";
}

public sealed class RabbitMqSettings
{
    public string Host { get; set; } = "rabbitmq";
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = "admin123";
}

/// <summary>
/// Best-effort publish of agent sec-events to <c>monitra.observations</c> for MngAlarm.
/// Observation keys: optional semantic (e.g. rdp.logon) else package id; Event ID stays in dimensions.
/// </summary>
public sealed class ObservationPublishSettings
{
    public bool Enabled { get; set; }

    /// <summary>
    /// Case-insensitive sourceProduct allowlist.
    /// Empty or <c>*</c> = publish all packages that stamp SourceProduct.
    /// </summary>
    public List<string> SourceProducts { get; set; } = ["*"];
}
