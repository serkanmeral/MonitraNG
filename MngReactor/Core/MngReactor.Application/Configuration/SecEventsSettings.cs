namespace MngReactor.Application.Configuration;

/// <summary>
/// sec_events ingest, saklama ve sorgu davranışı (SIEM_PERFORMANCE_PLAN §2.4, SIEM_PLANNING §9).
/// </summary>
public class SecEventsSettings
{
    /// <summary>
    /// Sıcak katman TTL (gün). @timestamp alanına Mongo TTL indeksi. 0 = TTL kapalı.
    /// </summary>
    public int HotTtlDays { get; set; } = 60;

    /// <summary>
    /// Parse edilemeyen / event.action=unknown olayları Mongo'ya yazma (observation da yok).
    /// </summary>
    public bool DropUnknownEvents { get; set; } = true;

    /// <summary>
    /// When false (default), reject legacy NXLog payloads (product windows-nxlog / EventID+Hostname JSON).
    /// Windows/Linux host events must arrive via MngLogs agent → LogCollector, not NXLog → Engine → Reactor.
    /// </summary>
    public bool AcceptNxlogIngest { get; set; }

    /// <summary>
    /// When false (default), reject legacy Linux UDP syslog (product linux-syslog / sshd|sudo lines).
    /// Linux host events must arrive via MngLogs agent (journal) → LogCollector.
    /// </summary>
    public bool AcceptLinuxSyslogIngest { get; set; }

    /// <summary>
    /// Tam ham mesajı (raw) Mongo'da sakla. false ise yalnızca rawPreview (512 byte).
    /// </summary>
    public bool PersistFullRaw { get; set; } = false;

    /// <summary>
    /// GET dashboard-summary yanıt önbelleği (saniye). 0 = kapalı.
    /// </summary>
    public int DashboardSummaryCacheSeconds { get; set; } = 60;

    /// <summary>
    /// Saatlik rollup koleksiyonundan dashboard-summary oku (Faz 2.2). Veri yoksa aggregation fallback.
    /// </summary>
    public bool UseDashboardHourlyRollup { get; set; } = true;

    /// <summary>
    /// After Mongo insert, also write normalized sec_events to OpenSearch (G1 fanout). Best-effort.
    /// </summary>
    public bool OpenSearchDualWriteEnabled { get; set; }

    /// <summary>
    /// When true, Query / GetById / dashboard-summary read from OpenSearch (G2). Mongo write path unchanged.
    /// </summary>
    public bool OpenSearchReadEnabled { get; set; }

    /// <summary>
    /// OpenSearch base URL (e.g. http://opensearch:9200). Used when dual-write or read is enabled.
    /// </summary>
    public string OpenSearchUrl { get; set; } = "http://opensearch:9200";
}
