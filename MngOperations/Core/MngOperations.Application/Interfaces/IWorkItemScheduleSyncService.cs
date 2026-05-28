using MngOperations.Application.Contracts.Schedules;

namespace MngOperations.Application.Interfaces;

public interface IWorkItemScheduleSyncService
{
    Task<WorkItemScheduleSyncResponse> SyncSchedulerJobAsync(string scheduleId, CancellationToken cancellationToken = default);

    Task UnlinkSchedulerJobAsync(string scheduleId, CancellationToken cancellationToken = default);
}
