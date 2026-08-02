namespace MngLogCollector.Application.Contracts.Discovery;

public sealed class DiscoveryHostListResponse
{
    public string DomainId { get; set; } = string.Empty;
    public int Total { get; set; }
    public IReadOnlyList<DiscoveryHostDto> Items { get; set; } = [];
    /// <summary>Configured site/subnet prefix table used for grouping (LPM).</summary>
    public IReadOnlyList<DiscoveryPrefixDto> Prefixes { get; set; } = [];
}

public sealed class DiscoveryPrefixDto
{
    public string Cidr { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? VlanName { get; set; }
}

public sealed class DiscoveryHostDto
{
    public string Id { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string Ip { get; set; } = string.Empty;
    /// <summary>windows | linux | unknown, or AD OS string when no scan hint.</summary>
    public string? OsHint { get; set; }
    public IReadOnlyList<int> OpenPorts { get; set; } = [];
    public string? DeviceRoleHint { get; set; }
    public string? IdentityConfidence { get; set; }
    public string? IdentitySummary { get; set; }
    public string? HttpTitle { get; set; }
    public string? TlsCommonName { get; set; }
    public string? SshBanner { get; set; }
    public string? SubnetCidr { get; set; }
    public string? SiteLabel { get; set; }
    public string? VlanName { get; set; }
    public IReadOnlyList<string> Sources { get; set; } = [];
    public string SamAccountName { get; set; } = string.Empty;
    public DateTime? LastSeenFromAd { get; set; }
    public DateTime? LastSeenFromScan { get; set; }
}

public sealed class DiscoveryScanStartRequest
{
    public string? DomainId { get; set; }
    /// <summary>CIDR (192.168.20.0/24) or range (192.168.20.1-254).</summary>
    public string Cidr { get; set; } = string.Empty;
    public bool EnrichWithAd { get; set; }
}

public sealed class DiscoveryScanStartResponse
{
    public string RunId { get; set; } = string.Empty;
    public string Status { get; set; } = "queued";
    public int TotalTargets { get; set; }
    public string? Error { get; set; }
}

public sealed class DiscoveryScanStatusResponse
{
    public string RunId { get; set; } = string.Empty;
    public string DomainId { get; set; } = string.Empty;
    public string Cidr { get; set; } = string.Empty;
    public bool EnrichWithAd { get; set; }
    public string Status { get; set; } = "queued";
    public int ProgressPercent { get; set; }
    public int TotalTargets { get; set; }
    public int Probed { get; set; }
    public int FoundAlive { get; set; }
    public int FoundWindows { get; set; }
    public int FoundLinux { get; set; }
    public int FoundUnknown { get; set; }
    public int Upserted { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public sealed class DiscoverySummaryResponse
{
    public string DomainId { get; set; } = string.Empty;
    public int TotalHosts { get; set; }
    public Dictionary<string, int> BySource { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTime? LastSyncAt { get; set; }
    public string LastSyncStatus { get; set; } = "never";
    public string? LastSyncError { get; set; }
    public DiscoverySyncStatsDto LastSyncStats { get; set; } = new();
}

public sealed class DiscoverySyncStatsDto
{
    public int Pulled { get; set; }
    public int Upserted { get; set; }
    public long DurationMs { get; set; }
}

public sealed class DiscoverySyncRequest
{
    /// <summary>Domain name (e.g. odak). Empty = sync all domains with LDAP enabled.</summary>
    public string? DomainId { get; set; }

    /// <summary>Discovery source. MVP: ad only.</summary>
    public string Source { get; set; } = "ad";
}

public sealed class DiscoverySyncResponse
{
    public string RunId { get; set; } = string.Empty;
    public string Status { get; set; } = "ok";
    public int Pulled { get; set; }
    public int Upserted { get; set; }
    public long DurationMs { get; set; }
    public string? Error { get; set; }
    public IReadOnlyList<DiscoverySyncDomainResult> Domains { get; set; } = [];
}

public sealed class DiscoverySyncDomainResult
{
    public string DomainId { get; set; } = string.Empty;
    public string Status { get; set; } = "ok";
    public int Pulled { get; set; }
    public int Upserted { get; set; }
    public long DurationMs { get; set; }
    public string? Error { get; set; }
}

public sealed class DiscoveryClearRequest
{
    public string? DomainId { get; set; }

    /// <summary>Optional source filter: scan | ad. Empty = delete all hosts for the domain.</summary>
    public string? Source { get; set; }
}

public sealed class DiscoveryClearResponse
{
    public string DomainId { get; set; } = string.Empty;
    public string Status { get; set; } = "ok";
    public string? Source { get; set; }
    public long Deleted { get; set; }
    public string? Error { get; set; }
}

public sealed class DiscoveryPrefixesResponse
{
    public string DomainId { get; set; } = string.Empty;
    /// <summary>config | mongo</summary>
    public string Source { get; set; } = "config";
    public IReadOnlyList<DiscoveryPrefixDto> Prefixes { get; set; } = [];
    public string? Error { get; set; }
}

public sealed class DiscoveryPrefixesPutRequest
{
    public IReadOnlyList<DiscoveryPrefixDto> Prefixes { get; set; } = [];
}
