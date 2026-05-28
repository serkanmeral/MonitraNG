using MngOperations.Application.Contracts.Schedules;

namespace MngOperations.Application.Interfaces;

public interface IWorkItemScheduleExecuteService
{
    Task<WorkItemScheduleExecuteResponse> ExecuteAsync(string scheduleId, CancellationToken cancellationToken = default);
}
