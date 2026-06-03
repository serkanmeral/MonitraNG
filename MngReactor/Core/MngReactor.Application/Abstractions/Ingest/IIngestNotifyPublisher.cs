namespace MngReactor.Application.Abstractions.Ingest;

/// <summary>
/// Ingest başarılı olduktan sonra UI için tek, throttle'lu "data.updated" event'i yayınlar.
/// </summary>
public interface IIngestNotifyPublisher
{
    /// <summary>
    /// Domain bazlı throttle uygular; süre dolmuşsa mng.topics'e monitoring.data.updated.{domain} yayınlar.
    /// </summary>
    Task TryPublishDataUpdatedAsync(string domain, DateTime lastIngestAtUtc, IReadOnlyList<string> engineIds, CancellationToken cancellationToken = default);
}
