namespace MngScheduler.Application.Interfaces;

/// <summary>
/// Job synchronization service interface
/// Used to trigger immediate sync from API controllers
/// </summary>
public interface IJobSyncService
{
    /// <summary>
    /// Trigger immediate job synchronization
    /// </summary>
    Task SyncNowAsync();
}
