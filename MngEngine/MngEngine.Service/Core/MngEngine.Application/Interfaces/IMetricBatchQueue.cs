using MngEngine.Application.Features.Ingest;

namespace MngEngine.Application.Interfaces;

/// <summary>
/// Collector sonuçlarının eklendiği in-memory queue.
/// SendJob bu queue'dan batch alıp Reactor'a gönderir.
/// </summary>
public interface IMetricBatchQueue
{
    /// <summary>
    /// Batch'i queue'ya ekler.
    /// </summary>
    void Enqueue(IngestBatch batch);

    /// <summary>
    /// Mümkün olan tüm batch'leri alır ve queue'dan çıkarır.
    /// </summary>
    IReadOnlyList<IngestBatch> DequeueAll();

    /// <summary>
    /// Queue'daki batch sayısı.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Queue içeriğini kopyasını döner (tüketmeden).
    /// </summary>
    IReadOnlyList<IngestBatch> PeekAll();

    /// <summary>
    /// Asset ID için son başarılı toplama zamanı (Enqueue sırasında güncellenir).
    /// Batch gönderilip queue'dan çıksa bile son bilinen zaman tutulur.
    /// </summary>
    IReadOnlyDictionary<string, DateTime> GetLastCollectedByAsset();
}
