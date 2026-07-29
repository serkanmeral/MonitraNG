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
