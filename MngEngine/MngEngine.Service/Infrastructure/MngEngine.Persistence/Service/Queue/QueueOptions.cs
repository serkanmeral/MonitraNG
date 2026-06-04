namespace MngEngine.Persistence.Service.Queue;

/// <summary>
/// Metric batch queue konfigürasyonu.
/// appsettings "MngEngine:Queue" veya QUEUE_MAX_BATCHES env ile ayarlanabilir.
/// </summary>
public class QueueOptions
{
    public const string SectionName = "MngEngine:Queue";

    /// <summary>
    /// Kuyrukta tutulacak maksimum batch sayısı.
    /// Limit aşıldığında en eski batch'ler atılır, en yeniler tutulur.
    /// 0 veya negatif = sınırsız (önerilmez, bellek tüketimine dikkat).
    /// Varsayılan: 1000
    /// </summary>
    public int MaxBatches { get; set; } = 1000;
}
