using System.Text.Json;

namespace MngLogCollector.Application.Contracts.Ingest;

public sealed class IngestBatchRequest
{
    /// <summary>Tenant/domain used for OpenSearch index naming (mng-{domain}-sec-events-…).</summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>Stable host/agent identifier from the field agent.</summary>
    public string HostId { get; set; } = string.Empty;

    public string? Hostname { get; set; }

    public IList<IngestEventItem> Events { get; set; } = [];
}

public sealed class IngestEventItem
{
    /// <summary>Optional client-supplied id; generated when empty.</summary>
    public string? Id { get; set; }

    public DateTime? TimestampUtc { get; set; }

    /// <summary>e.g. windows-eventlog, linux-journal, metric</summary>
    public string Source { get; set; } = "unknown";

    public string? SourceProduct { get; set; }

    public string? Severity { get; set; }

    public string? Message { get; set; }

    /// <summary>Raw payload as JSON (kept for later normalize).</summary>
    public JsonElement? Raw { get; set; }

    /// <summary>Optional pre-normalized fields from agent (MVP passthrough).</summary>
    public Dictionary<string, object?>? Fields { get; set; }
}

public sealed class IngestBatchResponse
{
    public int Accepted { get; set; }
    public int Written { get; set; }
    public bool OpenSearchWriteEnabled { get; set; }
}
