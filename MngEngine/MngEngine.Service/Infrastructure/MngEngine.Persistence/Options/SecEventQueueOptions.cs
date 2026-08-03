namespace MngEngine.Persistence.Options;

/// <summary>Syslog listener + sec-event kuyruk ayarları (SIEM Faz 1 S3).</summary>
public sealed class SecEventQueueOptions
{
    public const string SectionName = "MngEngine:SecEventQueue";

    public bool Enabled { get; set; } = true;

    /// <summary>UDP syslog dinleme portu. Prod: 514; geliştirme: 5514 (admin gerekmez).</summary>
    public int UdpPort { get; set; } = 5514;

    /// <summary>Çoklu UDP dinleyici (IT sabit portları). Boşsa <see cref="UdpPort"/> kullanılır.</summary>
    public List<SecEventSyslogListenerOptions>? UdpListeners { get; set; }

    public IReadOnlyList<SecEventSyslogListenerOptions> GetEffectiveListeners()
    {
        IReadOnlyList<SecEventSyslogListenerOptions> listeners = UdpListeners is { Count: > 0 }
            ? UdpListeners
            :
            [
                new SecEventSyslogListenerOptions
                {
                    UdpPort = UdpPort,
                    SourceType = DefaultSourceType,
                    SourceProduct = DefaultSourceProduct
                }
            ];

        return listeners
            .Where(l =>
                (AcceptNxlogIngest || !IsNxlogListenerProduct(l.SourceProduct))
                && (AcceptLinuxSyslogIngest || !IsLinuxSyslogListenerProduct(l.SourceProduct)))
            .ToList();
    }

    private static bool IsNxlogListenerProduct(string? product) =>
        !string.IsNullOrWhiteSpace(product)
        && (product.Equals("windows-nxlog", StringComparison.OrdinalIgnoreCase)
            || product.Equals("windows-nxlog-json", StringComparison.OrdinalIgnoreCase));

    private static bool IsLinuxSyslogListenerProduct(string? product) =>
        !string.IsNullOrWhiteSpace(product)
        && product.Equals("linux-syslog", StringComparison.OrdinalIgnoreCase);

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

    /// <summary>
    /// When false (default), drop NXLog JSON / windows-nxlog listeners and WEC items that look like NXLog.
    /// Host Windows/Linux telemetry must use MngLogs agent.
    /// </summary>
    public bool AcceptNxlogIngest { get; set; }

    /// <summary>
    /// When false (default), do not bind linux-syslog UDP listeners and drop sshd/sudo syslog payloads.
    /// Linux host telemetry must use MngLogs agent (journal).
    /// </summary>
    public bool AcceptLinuxSyslogIngest { get; set; }

    /// <summary>WEC HTTP batch ingest (POST /api/SecEvents/wec-batch). Default false — was NXLog om_http path.</summary>
    public bool WecIngestEnabled { get; set; }

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
