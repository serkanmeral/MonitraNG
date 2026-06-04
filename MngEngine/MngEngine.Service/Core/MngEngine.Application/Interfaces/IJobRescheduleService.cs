namespace MngEngine.Application.Interfaces;

/// <summary>
/// Config'e göre Quartz job'larını (CollectorJob, SendJob, ConfigSyncJob) yeniden zamanlar.
/// Sync sonrası (MQTT veya periyodik) çağrılır.
/// </summary>
public interface IJobRescheduleService
{
    Task RescheduleJobsAsync(CancellationToken cancellationToken = default);
}
