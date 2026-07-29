namespace MngLogCollector.Application.Configuration;

public sealed class MngLogCollectorSettings
{
    public const string SectionName = "MngLogCollectorSettings";

    public ServerSettings Server { get; set; } = new();
    public OpenSearchSettings OpenSearch { get; set; } = new();
    public IngestSettings Ingest { get; set; } = new();
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
