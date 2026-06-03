namespace MngReactor.Application.Abstractions.Domain;

/// <summary>
/// Yeni tenant için varsayılan monitoring kayıtlarını oluşturur (mon_schedules, mon_collection_periods).
/// RabbitMQ domain.created event veya manuel init API ile tetiklenir.
/// </summary>
public interface IDomainDefaultsService
{
    /// <summary>
    /// mng_{domain} veritabanında mon_schedules ("Sürekli") ve mon_collection_periods ("1 dakika") oluşturur.
    /// </summary>
    Task<bool> CreateDefaultsAsync(string domainName, string? accessToken = null, CancellationToken cancellationToken = default);
}
