using MngAlarm.Application.Contracts;
using MngAlarm.Application.Services;
using MngAlarm.Domain.Entities;
using MngAlarm.Domain.Enums;

namespace MngAlarm.Infrastructure.Services;

public sealed class AlarmQueryService(IAlarmDomainAccessor domain, IAlarmRepository alarms) : IAlarmQueryService
{
    public async Task<AlarmListResponse> ListAsync(
        AlarmStatus? status,
        int? minSeverity,
        bool openOnly,
        int skip,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var ctx = domain.GetRequiredDomain();
        var safeSkip = Math.Max(0, skip);
        var safeLimit = Math.Clamp(limit <= 0 ? 50 : limit, 1, 200);

        var (items, total) = await alarms.ListAsync(
            ctx.DomainName,
            status,
            minSeverity,
            openOnly,
            safeSkip,
            safeLimit,
            cancellationToken);

        return new AlarmListResponse
        {
            Items = items.Select(Map).ToList(),
            Total = total,
            Skip = safeSkip,
            Limit = safeLimit
        };
    }

    public async Task<AlarmSummaryDto?> GetAsync(string alarmId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(alarmId))
            return null;

        var ctx = domain.GetRequiredDomain();
        var alarm = await alarms.GetByIdAsync(ctx.DomainName, alarmId.Trim(), cancellationToken);
        return alarm == null ? null : Map(alarm);
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
        Context = alarm.Context
    };
}
