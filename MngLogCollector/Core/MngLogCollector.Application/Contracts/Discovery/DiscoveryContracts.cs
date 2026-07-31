namespace MngLogCollector.Application.Contracts.Discovery;

public sealed class DiscoveryHostListResponse
{
    public string DomainId { get; set; } = string.Empty;
    public int Total { get; set; }
    public IReadOnlyList<DiscoveryHostDto> Items { get; set; } = [];
}

public sealed class DiscoveryHostDto
{
    public string Id { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string Ip { get; set; } = string.Empty;
    public string? OsHint { get; set; }
    public IReadOnlyList<string> Sources { get; set; } = [];
    public string SamAccountName { get; set; } = string.Empty;
    public DateTime? LastSeenFromAd { get; set; }
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
