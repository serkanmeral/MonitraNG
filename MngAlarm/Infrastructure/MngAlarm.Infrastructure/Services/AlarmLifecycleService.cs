using MngAlarm.Application.Contracts;
using MngAlarm.Application.Observations;
using MngAlarm.Application.Services;
using MngAlarm.Domain.Constants;
using MngAlarm.Domain.Entities;
using MngAlarm.Domain.Enums;

namespace MngAlarm.Infrastructure.Services;

public sealed class AlarmLifecycleService(
    IAlarmDomainAccessor domain,
    IAlarmActorAccessor actorAccessor,
    IAlarmRepository alarms,
    IAlarmEventPublisher publisher,
    IAlarmNotificationDispatchService notificationDispatch) : IAlarmLifecycleService
{
    public Task<AlarmSummaryDto?> AcknowledgeAsync(string alarmId, CancellationToken cancellationToken = default) =>
        TransitionAsync(
            alarmId,
            from: [AlarmStatus.Active],
            to: AlarmStatus.Acknowledged,
            AlarmEventTypes.Updated,
            cancellationToken);

    public Task<AlarmSummaryDto?> SuppressAsync(string alarmId, CancellationToken cancellationToken = default) =>
        TransitionAsync(
            alarmId,
            from: [AlarmStatus.Active, AlarmStatus.Acknowledged],
            to: AlarmStatus.Suppressed,
            AlarmEventTypes.Updated,
            cancellationToken);

    public Task<AlarmSummaryDto?> ResolveAsync(string alarmId, CancellationToken cancellationToken = default) =>
        TransitionAsync(
            alarmId,
            from: [AlarmStatus.Active, AlarmStatus.Acknowledged, AlarmStatus.Suppressed],
            to: AlarmStatus.Resolved,
            AlarmEventTypes.Resolved,
            cancellationToken);

    private async Task<AlarmSummaryDto?> TransitionAsync(
        string alarmId,
        AlarmStatus[] from,
        AlarmStatus to,
        string lifecycle,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(alarmId))
            return null;

        var ctx = domain.GetRequiredDomain();
        var alarm = await alarms.GetByIdAsync(ctx.DomainName, alarmId.Trim(), cancellationToken);
        if (alarm == null || !from.Contains(alarm.Status))
            return null;

        var previousStatus = alarm.Status;
        var actor = actorAccessor.GetCurrentActor();
        var now = DateTime.UtcNow;
        alarm.Status = to;
        alarm.LastSeenAt = now;
        alarm.LastPublishedAt = now;
        AlarmLifecycleHistoryHelper.AppendManualEntry(alarm.Context, previousStatus, to, actor);

        await alarms.UpdateAsync(alarm, cancellationToken);
        await PublishEventAsync(alarm, lifecycle, cancellationToken);
        return Map(alarm);
    }

    private async Task PublishEventAsync(AlarmDocument alarm, string lifecycle, CancellationToken cancellationToken)
    {
        var message = new AlarmEventMessage
        {
            DomainId = alarm.DomainId,
            DomainName = alarm.DomainName,
            EventType = AlarmLifecycleMapper.ToPayloadEventType(lifecycle),
            AlarmId = alarm.Id,
            RuleId = alarm.RuleId,
            Severity = alarm.Severity,
            DedupKey = alarm.DedupKey,
            Context = alarm.Context,
            CorrelationId = alarm.CorrelationId,
            EventId = $"{alarm.Id}:{lifecycle}:manual:{DateTime.UtcNow.Ticks}",
        };

        await publisher.PublishAsync(message, lifecycle, cancellationToken);
        await notificationDispatch.DispatchAsync(message, cancellationToken);
    }

    private static AlarmSummaryDto Map(AlarmDocument alarm) => new()
    {
        Id = alarm.Id,
        RuleId = alarm.RuleId,
        DedupKey = alarm.DedupKey,
        DomainId = alarm.DomainId,
        DomainName = alarm.DomainName,
        Severity = alarm.Severity,
        Status = alarm.Status.ToString(),
        FirstSeenAt = alarm.FirstSeenAt,
        LastSeenAt = alarm.LastSeenAt,
        Count = alarm.Count,
        CorrelationId = alarm.CorrelationId,
        Context = AlarmContextApiNormalizer.ForApi(alarm.Context),
    };
}
