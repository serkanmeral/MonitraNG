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
}
