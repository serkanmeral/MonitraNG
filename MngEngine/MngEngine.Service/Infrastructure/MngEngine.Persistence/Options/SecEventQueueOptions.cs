namespace MngEngine.Persistence.Options;

/// <summary>Syslog listener + sec-event kuyruk ayarları (SIEM Faz 1 S3).</summary>
public sealed class SecEventQueueOptions
{
    public const string SectionName = "MngEngine:SecEventQueue";

    public bool Enabled { get; set; } = true;

    /// <summary>UDP syslog dinleme portu. Prod: 514; geliştirme: 5514 (admin gerekmez).</summary>
    public int UdpPort { get; set; } = 5514;

    /// <summary>Kuyruk üst sınırı; aşıldığında en eski öğeler atılır.</summary>
    public int MaxItems { get; set; } = 5000;

    /// <summary>Bu eşiğe ulaşınca anında flush tetiklenir.</summary>
    public int BatchThreshold { get; set; } = 100;

    /// <summary>SecEventSendJob periyodu (saniye).</summary>
    public int SendIntervalSeconds { get; set; } = 30;

    public string DefaultSourceType { get; set; } = "firewall";

    public string DefaultSourceProduct { get; set; } = "generic-syslog";

    public string? DefaultSourceHost { get; set; }

    /// <summary>Tek syslog datagram üst sınırı (byte).</summary>
    public int MaxMessageBytes { get; set; } = 8192;

    /// <summary>WEC HTTP batch ingest (POST /api/SecEvents/wec-batch).</summary>
    public bool WecIngestEnabled { get; set; } = true;

    /// <summary>WEC batch source.host varsayılanı (forwarder host adı).</summary>
    public string? DefaultWecHost { get; set; }

    /// <summary>POST /api/SecEvents/wec-batch tek istekte kabul edilen maksimum olay sayısı.</summary>
    public int MaxWecBatchItems { get; set; } = 500;

    /// <summary>Reactor sec-events ingest'e tek HTTP isteğinde gönderilen maksimum olay.</summary>
    public int MaxReactorBatchItems { get; set; } = 200;

    /// <summary>Reactor gönderimi başarısız olursa yeniden deneme sayısı (0 = tek deneme).</summary>
    public int ReactorSendRetryCount { get; set; } = 3;

    /// <summary>Reactor yeniden denemeleri arası bekleme (ms).</summary>
    public int ReactorSendRetryDelayMs { get; set; } = 500;
}
