using System.Text.Json;

namespace MngLogs.Agent.Contracts;

/// <summary>Wire format for MngLogCollector ingest API (kept local to the field agent).</summary>
public sealed class IngestBatchRequest
{
    public string Domain { get; set; } = string.Empty;
    public string HostId { get; set; } = string.Empty;
    public string? Hostname { get; set; }
    public IList<IngestEventItem> Events { get; set; } = [];
}

public sealed class IngestEventItem
{
    public string? Id { get; set; }
    public DateTime? TimestampUtc { get; set; }
    public string Source { get; set; } = "unknown";
    public string? SourceProduct { get; set; }
    public string? Severity { get; set; }
    public string? Message { get; set; }
    public JsonElement? Raw { get; set; }
    public Dictionary<string, object?>? Fields { get; set; }
}

public sealed class IngestBatchResponse
{
    public int Accepted { get; set; }
    public int Written { get; set; }
    public bool OpenSearchWriteEnabled { get; set; }
}

/// <summary>GET /api/v1/policy/eventlog-packages response from MngLogCollector.</summary>
public sealed class EventLogPackageCatalogResponse
{
    public string Version { get; set; } = string.Empty;
    public string Source { get; set; } = "collector";
    public DateTime GeneratedUtc { get; set; }
    public List<EventLogPackageCatalogItem> Packages { get; set; } = [];
    public List<EventLogPackageCatalogItem> OptionalPackages { get; set; } = [];
}

public sealed class EventLogPackageCatalogItem
{
    public string Name { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string SelectionMode { get; set; } = "selected";
    public List<int> EventIds { get; set; } = [];
    public List<int> ExcludedEventIds { get; set; } = [];
    public bool IsDefault { get; set; } = true;
}

/// <summary>Result of a catalog pull (200 body, 304 not modified, or failure).</summary>
public sealed class EventLogPackageCatalogPullResult
{
    public bool Success { get; init; }
    public bool NotModified { get; init; }
    public EventLogPackageCatalogResponse? Catalog { get; init; }

    public static EventLogPackageCatalogPullResult Failed() => new() { Success = false };
    public static EventLogPackageCatalogPullResult Unchanged() => new() { Success = true, NotModified = true };
    public static EventLogPackageCatalogPullResult Ok(EventLogPackageCatalogResponse catalog) =>
        new() { Success = true, Catalog = catalog };
}
