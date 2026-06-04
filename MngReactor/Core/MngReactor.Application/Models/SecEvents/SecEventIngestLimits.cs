namespace MngReactor.Application.Models.SecEvents;

/// <summary>
/// SIEM sec_events ingest performans sınırları (SIEM_PERFORMANCE_PLAN §2.3, IngestProcessing ile hizalı).
/// </summary>
public static class SecEventIngestLimits
{
    /// <summary>HTTP istek başına üst sınır — aşan batch reddedilir (413/400).</summary>
    public const int MaxItemsPerRequest = 5000;

    /// <summary>Mongo bulkWrite parça boyutu (metrik ingest ile aynı).</summary>
    public const int MongoBulkChunkSize = 1000;

    /// <summary>Ham mesaj önizleme üst sınırı (byte); tam raw Faz 2+.</summary>
    public const int MaxRawPreviewBytes = 512;

    /// <summary>Engine batch eşiği — olay sayısı (Engine S3).</summary>
    public const int EngineBatchEventThreshold = 100;

    /// <summary>Engine batch eşiği — bekleme süresi (saniye).</summary>
    public const int EngineBatchMaxWaitSeconds = 30;
}
