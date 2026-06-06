using MngAlarm.Application.Contracts;

namespace MngAlarm.Application.Services;

public interface IAlarmLifecycleService
{
    Task<AlarmSummaryDto?> AcknowledgeAsync(string alarmId, CancellationToken cancellationToken = default);
    Task<AlarmSummaryDto?> SuppressAsync(string alarmId, CancellationToken cancellationToken = default);
    Task<AlarmSummaryDto?> ResolveAsync(string alarmId, CancellationToken cancellationToken = default);
}
