using System.Text.Json.Nodes;

namespace MngReactor.Application.Abstractions.Ingest;

/// <summary>
/// mon_metrics Time Series koleksiyonuna dogrudan MongoDB yazimi.
/// Multi-tenant: database adi mng_{domain} formatinda (token'dan gelen domain).
/// </summary>
public interface IMonMetricsRepository
{
    /// <summary>
    /// Metrik dokümanlarini ilgili tenant veritabanina (mng_{domain}) yazar.
    /// Koleksiyon yoksa Time Series olarak olusturulur (TTL ile).
    /// </summary>
    /// <param name="domain">Token'dan gelen domain (ornek: meral → mng_meral)</param>
    /// <param name="documents">Yazilacak metrik dokümanlari (timestamp, meta, value, unit)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Basarili yazilan doküman sayisi</returns>
    Task<int> InsertManyAsync(string domain, IReadOnlyList<JsonObject> documents, CancellationToken ct = default);
}
