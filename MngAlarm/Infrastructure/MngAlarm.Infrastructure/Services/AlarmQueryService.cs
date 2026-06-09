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
        string? ruleId = null,
        string? search = null,
        DateTime? from = null,
        DateTime? to = null,
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
            string.IsNullOrWhiteSpace(ruleId) ? null : ruleId.Trim(),
            string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            from,
            to,
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

    public async Task<AlarmDashboardSnapshot> GetDashboardSnapshotAsync(
        int rangeHours = 24,
        int minSeverity = 6,
        int openLimit = 15,
        CancellationToken cancellationToken = default)
    {
        var ctx = domain.GetRequiredDomain();
        var hours = Math.Clamp(rangeHours, 1, 168);
        var to = DateTime.UtcNow;
        var from = to.AddHours(-hours);
        var safeLimit = Math.Clamp(openLimit <= 0 ? 15 : openLimit, 1, 200);

        var (openItems, openTotal) = await alarms.ListAsync(
            ctx.DomainName,
            status: null,
            minSeverity: minSeverity,
            openOnly: true,
            skip: 0,
            limit: safeLimit,
            ruleId: null,
            search: null,
            from: null,
            to: null,
            cancellationToken);

        var rollup = await alarms.GetScenarioRollupAsync(ctx.DomainName, from, to, cancellationToken);

        return new AlarmDashboardSnapshot
        {
            From = from,
            To = to,
            OpenTotal = openTotal,
            OpenAlarms = openItems.Select(Map).ToList(),
            ScenarioRollup = rollup.Select(r => new AlarmScenarioRollupDto
            {
                MatchKey = r.MatchKey,
                OpenCount = r.OpenCount,
                TotalInRange = r.TotalInRange,
                MaxSeverity = r.MaxSeverity,
                LastSeenAt = r.LastSeenAt,
            }).ToList(),
        };
    }

    public async Task<AlarmTrendBucketsResult> GetTrendBucketsAsync(
        int rangeHours = 24,
        CancellationToken cancellationToken = default)
    {
        var ctx = domain.GetRequiredDomain();
        var hours = Math.Clamp(rangeHours, 1, 168);
        var to = DateTime.UtcNow;
        var from = to.AddHours(-hours);
        var hourStarts = new List<DateTime>(hours);
        for (var idx = 0; idx < hours; idx++)
        {
            var bucketEnd = to.AddHours(-(hours - 1 - idx));
            hourStarts.Add(DateTime.SpecifyKind(bucketEnd.AddHours(-1), DateTimeKind.Utc));
        }

        var items = await alarms.GetTrendBucketsAsync(ctx.DomainName, from, to, hourStarts, cancellationToken);
        return new AlarmTrendBucketsResult
        {
            From = from,
            To = to,
            Items = items,
        };
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
        Context = AlarmContextApiNormalizer.ForApi(alarm.Context)
    };
}
