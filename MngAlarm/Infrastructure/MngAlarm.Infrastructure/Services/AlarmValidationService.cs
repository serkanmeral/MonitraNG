using System.Globalization;
using Microsoft.Extensions.Logging;
using MngAlarm.Application.Contracts;
using MngAlarm.Application.Services;
using MngAlarm.Domain.Constants;
using MngAlarm.Domain.Entities;
using MngAlarm.Domain.Enums;
using MngAlarm.Application.Observations;
using MngAlarm.Infrastructure.Evaluation;
using MngAlarm.Infrastructure.State;

namespace MngAlarm.Infrastructure.Services;

public sealed class AlarmValidationService : IAlarmValidationService
{
    private readonly IAlarmRuleRepository _rules;
    private readonly IAlarmRepository _alarms;
    private readonly IAlarmEventPublisher _publisher;
    private readonly IAlarmNotificationDispatchService _notificationDispatch;
    private readonly ICorrelationWindowStore _windows;
    private readonly IObservationActivityStore _activity;
    private readonly ILogger<AlarmValidationService> _logger;

    public AlarmValidationService(
        IAlarmRuleRepository rules,
        IAlarmRepository alarms,
        IAlarmEventPublisher publisher,
        IAlarmNotificationDispatchService notificationDispatch,
        ICorrelationWindowStore windows,
        IObservationActivityStore activity,
        ILogger<AlarmValidationService> logger)
    {
        _rules = rules;
        _alarms = alarms;
        _publisher = publisher;
        _notificationDispatch = notificationDispatch;
        _windows = windows;
        _activity = activity;
        _logger = logger;
    }

    public async Task<AlarmValidationScanResponse> RunScanAsync(
        string domainName,
        string domainId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var correlationResolved = await ResolveExpiredCorrelationAlarmsAsync(domainName, now, cancellationToken);
        var (scheduledRaised, scheduledResolved) = await ProcessScheduledRulesAsync(domainName, domainId, now, cancellationToken);
        var windowsPruned = _windows.PruneExpired(domainName, now);

        _logger.LogInformation(
            "Validation scan domain={Domain} correlationResolved={CorrelationResolved} scheduledRaised={ScheduledRaised} scheduledResolved={ScheduledResolved}",
            domainName,
            correlationResolved,
            scheduledRaised,
            scheduledResolved);

        return new AlarmValidationScanResponse
        {
            CorrelationResolved = correlationResolved,
            ScheduledRaised = scheduledRaised,
            ScheduledResolved = scheduledResolved,
            WindowsPruned = windowsPruned
        };
    }

    private async Task<int> ResolveExpiredCorrelationAlarmsAsync(
        string domainName,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var rules = await _rules.ListEnabledByTypeAsync(domainName, AlarmRuleTypes.Correlation, cancellationToken);
        var resolved = 0;

        foreach (var rule in rules)
        {
            var window = CorrelationEvaluator.GetWindow(rule);
            var activeAlarms = await _alarms.ListActiveByRuleIdAsync(domainName, rule.Id, cancellationToken);

            foreach (var alarm in activeAlarms)
            {
                var groupKey = alarm.Context.TryGetValue("groupKey", out var gk) ? gk?.ToString() ?? "_all" : "_all";
                var storeKey = CorrelationEvaluator.BuildStoreKey(domainName, rule.Id, groupKey);
                var count = _windows.GetCount(storeKey, now, window);

                if (CorrelationEvaluator.IsBreaching(count, rule.Threshold))
                    continue;

                alarm.Status = AlarmStatus.Resolved;
                alarm.LastSeenAt = now;
                alarm.Context["windowCount"] = count;
                alarm.Context["resolvedBy"] = "validation-scan";
                await _alarms.UpdateAsync(alarm, cancellationToken);
                await PublishEventAsync(alarm, AlarmEventTypes.Resolved, alarm.Context, cancellationToken);
                resolved++;

                _logger.LogInformation(
                    "Correlation alarm resolved by scan rule={RuleId} alarm={AlarmId} count={Count}",
                    rule.Id, alarm.Id, count);
            }
        }

        return resolved;
    }

    private async Task<(int Raised, int Resolved)> ProcessScheduledRulesAsync(
        string domainName,
        string domainId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var rules = await _rules.ListEnabledByTypeAsync(domainName, AlarmRuleTypes.Scheduled, cancellationToken);
        var raised = 0;
        var resolved = 0;

        foreach (var rule in rules)
        {
            if (rule.StalenessMinutes <= 0)
                continue;

            var staleness = TimeSpan.FromMinutes(rule.StalenessMinutes);
            var keys = _activity.EnumerateKeys(domainName, rule.Id).ToList();
            if (keys.Count == 0)
                keys.Add(CorrelationEvaluator.BuildActivityKey(domainName, rule.Id, "_all"));

            foreach (var activityKey in keys)
            {
                var groupKey = activityKey[(domainName.Length + rule.Id.Length + 2)..];
                var dedupKey = CorrelationEvaluator.BuildDedupKey(rule, groupKey);
                var existing = await _alarms.GetActiveByDedupKeyAsync(domainName, dedupKey, cancellationToken);
                var lastSeen = _activity.GetLastSeen(activityKey);
                var isStale = lastSeen == null || now - lastSeen.Value > staleness;

                if (isStale)
                {
                    if (existing != null)
                        continue;

                    var context = new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["key"] = rule.MatchKey,
                        ["groupKey"] = groupKey,
                        ["stalenessMinutes"] = rule.StalenessMinutes,
                        ["lastSeenAt"] = lastSeen?.ToString("O", CultureInfo.InvariantCulture),
                        ["detectedAt"] = now.ToString("O", CultureInfo.InvariantCulture),
                        ["reason"] = "observation_stale"
                    };

                    var alarm = new AlarmDocument
                    {
                        DomainId = domainId,
                        DomainName = domainName,
                        RuleId = rule.Id,
                        DedupKey = dedupKey,
                        Severity = rule.Severity,
                        Context = context,
                        FirstSeenAt = now,
                        LastSeenAt = now,
                        LastPublishedAt = now
                    };

                    await _alarms.InsertAsync(alarm, cancellationToken);
                    await PublishEventAsync(alarm, AlarmEventTypes.Raised, context, cancellationToken);
                    raised++;

                    _logger.LogInformation(
                        "Scheduled staleness alarm raised rule={RuleId} group={GroupKey}",
                        rule.Id, groupKey);
                    continue;
                }

                if (existing == null)
                    continue;

                existing.Status = AlarmStatus.Resolved;
                existing.LastSeenAt = now;
                existing.Context["lastSeenAt"] = lastSeen?.ToString("O", CultureInfo.InvariantCulture);
                existing.Context["resolvedBy"] = "validation-scan";
                await _alarms.UpdateAsync(existing, cancellationToken);
                await PublishEventAsync(existing, AlarmEventTypes.Resolved, existing.Context, cancellationToken);
                resolved++;
            }
        }

        return (raised, resolved);
    }

    private async Task PublishEventAsync(
        AlarmDocument alarm,
        string lifecycle,
        Dictionary<string, object?> context,
        CancellationToken cancellationToken)
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
            Context = context,
            CorrelationId = alarm.CorrelationId,
            EventId = $"{alarm.Id}:{lifecycle}:{DateTime.UtcNow.Ticks}"
        };

        await _publisher.PublishAsync(message, lifecycle, cancellationToken);
        await _notificationDispatch.DispatchAsync(message, cancellationToken);
    }
}
