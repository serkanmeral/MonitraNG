namespace MngEngine.Persistence.Options;

/// <summary>
/// EngineStatusJob (Health/Status heartbeat) konfigürasyonu.
/// appsettings "MngEngine:EngineStatusJob" veya MngEngine__EngineStatusJob__CronExpression env ile ayarlanabilir.
/// </summary>
public class EngineStatusJobOptions
{
    public const string SectionName = "MngEngine:EngineStatusJob";

    /// <summary>
    /// Quartz cron ifadesi (6 alan: saniye dakika saat gün ay haftagünü).
    /// Varsayılan: "0 */2 * * * ?" (her 2 dakikada bir).
    /// Örnek env: MngEngine__EngineStatusJob__CronExpression="0 */5 * * * ?"
    /// </summary>
    public string CronExpression { get; set; } = "0 */2 * * * ?";
}
