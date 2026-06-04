using System.Globalization;
using Microsoft.Extensions.Logging;
using MngAlarm.Application.Contracts;
using MngAlarm.Application.Observations;
using MngAlarm.Application.Services;
using MngAlarm.Domain.Constants;
using MngAlarm.Domain.Entities;
using MngAlarm.Domain.Enums;
using MngAlarm.Infrastructure.Evaluation;
using MngAlarm.Infrastructure.State;

namespace MngAlarm.Infrastructure.Services;

public sealed class ObservationProcessor : IObservationProcessor
{
    private readonly IAlarmRuleRepository _rules;
    private readonly IAlarmRepository _alarms;
    private readonly IAlarmEventPublisher _publisher;
    private readonly ICorrelationWindowStore _windows;
    private readonly ISequenceStateStore _sequences;
    private readonly IObservationActivityStore _activity;
    private readonly ILogger<ObservationProcessor> _logger;

    public ObservationProcessor(
        IAlarmRuleRepository rules,
        IAlarmRepository alarms,
        IAlarmEventPublisher publisher,
        ICorrelationWindowStore windows,
        ISequenceStateStore sequences,
        IObservationActivityStore activity,
        ILogger<ObservationProcessor> logger)
    {
        _rules = rules;
        _alarms = alarms;
        _publisher = publisher;
        _windows = windows;
        _sequences = sequences;
        _activity = activity;
        _logger = logger;
    }

    public async Task<AlarmProcessResult> ProcessAsync(ObservationEnvelope observation, CancellationToken cancellationToken = default)
    {
        var rules = await _rules.ListEnabledByKeyAsync(observation.DomainName, observation.Key, cancellationToken);
        var sequenceRules = await _rules.ListEnabledByTypeAsync(
            observation.DomainName,
            AlarmRuleTypes.Sequence,
            cancellationToken);
        var raised = 0;
        var updated = 0;
        var resolved = 0;
        var alarmIds = new List<string>();
        var rulesEvaluated = rules.Count;

        foreach (var rule in rules)
        {
            if (string.Equals(rule.Type, AlarmRuleTypes.Correlation, StringComparison.Ordinal))
            {
                var outcome = await ProcessCorrelationAsync(rule, observation, cancellationToken);
                raised += outcome.Raised;
                updated += outcome.Updated;
                resolved += outcome.Resolved;
                alarmIds.AddRange(outcome.AlarmIds);
                continue;
            }

            if (!string.Equals(rule.Type, AlarmRuleTypes.Threshold, StringComparison.Ordinal))
            {
                RecordScheduledActivity(rule, observation);
                continue;
            }

            var thresholdOutcome = await ProcessThresholdAsync(rule, observation, cancellationToken);
            raised += thresholdOutcome.Raised;
            updated += thresholdOutcome.Updated;
            resolved += thresholdOutcome.Resolved;
            alarmIds.AddRange(thresholdOutcome.AlarmIds);
            RecordScheduledActivity(rule, observation);
        }

        foreach (var rule in sequenceRules)
        {
            if (!rule.Enabled || !SequenceEvaluator.IsValidRule(rule))
                continue;

            rulesEvaluated++;
            var outcome = await ProcessSequenceAsync(rule, observation, cancellationToken);
            raised += outcome.Raised;
            updated += outcome.Updated;
            resolved += outcome.Resolved;
            alarmIds.AddRange(outcome.AlarmIds);
        }

        return new AlarmProcessResult
        {
            RulesEvaluated = rulesEvaluated,
            AlarmsRaised = raised,
            AlarmsUpdated = updated,
            AlarmsResolved = resolved,
            AlarmIds = alarmIds
        };
    }

    private void RecordScheduledActivity(AlarmRuleDocument rule, ObservationEnvelope observation)
    {
        if (rule.StalenessMinutes <= 0)
            return;

        var groupKey = CorrelationEvaluator.BuildGroupKey(rule, observation.Dimensions);
        var activityKey = CorrelationEvaluator.BuildActivityKey(observation.DomainName, rule.Id, groupKey);
        _activity.Record(activityKey, observation.Timestamp);
    }

    private async Task<RuleOutcome> ProcessThresholdAsync(
        AlarmRuleDocument rule,
        ObservationEnvelope observation,
        CancellationToken cancellationToken)
    {
        var dedupKey = ThresholdEvaluator.BuildDedupKey(rule, observation.Key);
        var existing = await _alarms.GetActiveByDedupKeyAsync(observation.DomainName, dedupKey, cancellationToken);
        var matches = ThresholdEvaluator.Matches(rule, observation.Value);
        var now = DateTime.UtcNow;
        var context = BuildThresholdContext(observation);

        if (!matches)
        {
            if (existing == null)
                return RuleOutcome.Empty;

            return await ResolveAsync(existing, context, rule, observation.Key, cancellationToken);
        }

        if (existing != null)
        {
            if (IsInCooldown(rule, existing, now))
                return RuleOutcome.Empty;

            return await UpdateAsync(existing, context, rule, observation.Key, cancellationToken);
        }

        return await RaiseAsync(rule, observation, dedupKey, context, observation.Key, cancellationToken);
    }

    private async Task<RuleOutcome> ProcessCorrelationAsync(
        AlarmRuleDocument rule,
        ObservationEnvelope observation,
        CancellationToken cancellationToken)
    {
        if (!CorrelationEvaluator.MatchesEvent(rule, observation))
            return RuleOutcome.Empty;

        var groupKey = CorrelationEvaluator.BuildGroupKey(rule, observation.Dimensions);
        var storeKey = CorrelationEvaluator.BuildStoreKey(observation.DomainName, rule.Id, groupKey);
        var window = CorrelationEvaluator.GetWindow(rule);
        var count = _windows.RecordAndCount(storeKey, observation.Timestamp, window);

        var activityKey = CorrelationEvaluator.BuildActivityKey(observation.DomainName, rule.Id, groupKey);
        _activity.Record(activityKey, observation.Timestamp);

        var dedupKey = CorrelationEvaluator.BuildDedupKey(rule, groupKey);
        var existing = await _alarms.GetActiveByDedupKeyAsync(observation.DomainName, dedupKey, cancellationToken);
        var now = DateTime.UtcNow;
        var context = CorrelationEvaluator.BuildContext(observation, groupKey, count);

        if (!CorrelationEvaluator.IsBreaching(count, rule.Threshold))
            return RuleOutcome.Empty;

        if (existing != null)
        {
            if (IsInCooldown(rule, existing, now))
                return RuleOutcome.Empty;

            return await UpdateAsync(existing, context, rule, observation.Key, cancellationToken);
        }

        return await RaiseAsync(rule, observation, dedupKey, context, observation.Key, cancellationToken);
    }

    private async Task<RuleOutcome> ProcessSequenceAsync(
        AlarmRuleDocument rule,
        ObservationEnvelope observation,
        CancellationToken cancellationToken)
    {
        var step0 = rule.SequenceSteps[0];
        var step1 = rule.SequenceSteps[1];
        var groupKey = CorrelationEvaluator.BuildGroupKey(rule, observation.Dimensions);
        var storeKey = SequenceEvaluator.BuildStoreKey(observation.DomainName, rule.Id, groupKey);
        var state = _sequences.GetOrCreate(storeKey);
        var now = DateTime.UtcNow;

        if (string.Equals(observation.Key, step0.MatchKey, StringComparison.Ordinal))
        {
            var windowMinutes = step0.WithinMinutes > 0 ? step0.WithinMinutes : rule.WindowMinutes;
            var window = TimeSpan.FromMinutes(Math.Max(1, windowMinutes));
            var windowKey = SequenceEvaluator.BuildStepWindowKey(storeKey, 0);
            var count = _windows.RecordAndCount(windowKey, observation.Timestamp, window);

            if (count == 1 || !state.AnchorTime.HasValue)
                state.AnchorTime = observation.Timestamp;

            if (!state.Armed && count >= Math.Max(1, step0.MinCount))
                state.Armed = true;

            _sequences.Save(storeKey, state);
            return RuleOutcome.Empty;
        }

        if (!string.Equals(observation.Key, step1.MatchKey, StringComparison.Ordinal)
            || !state.Armed
            || !state.AnchorTime.HasValue)
        {
            return RuleOutcome.Empty;
        }

        var deadlineMinutes = step1.WithinMinutesAfterFirst > 0
            ? step1.WithinMinutesAfterFirst
            : Math.Max(1, rule.WindowMinutes);
        if (observation.Timestamp > state.AnchorTime.Value.AddMinutes(deadlineMinutes))
        {
            _sequences.Reset(storeKey);
            return RuleOutcome.Empty;
        }

        var dedupKey = CorrelationEvaluator.BuildDedupKey(rule, groupKey);
        var existing = await _alarms.GetActiveByDedupKeyAsync(observation.DomainName, dedupKey, cancellationToken);
        if (existing != null)
        {
            if (IsInCooldown(rule, existing, now))
            {
                _sequences.Reset(storeKey);
                return RuleOutcome.Empty;
            }

            var updateContext = SequenceEvaluator.BuildContext(
                rule,
                observation,
                groupKey,
                state,
                step0.MinCount);
            var updated = await UpdateAsync(existing, updateContext, rule, rule.MatchKey, cancellationToken);
            _sequences.Reset(storeKey);
            return updated;
        }

        var priorCount = _windows.GetCount(
            SequenceEvaluator.BuildStepWindowKey(storeKey, 0),
            observation.Timestamp,
            TimeSpan.FromMinutes(Math.Max(1, step0.WithinMinutes > 0 ? step0.WithinMinutes : rule.WindowMinutes)));
        var context = SequenceEvaluator.BuildContext(rule, observation, groupKey, state, priorCount);
        var raised = await RaiseAsync(rule, observation, dedupKey, context, rule.MatchKey, cancellationToken);
        _sequences.Reset(storeKey);
        return raised;
    }

    private async Task<RuleOutcome> RaiseAsync(
        AlarmRuleDocument rule,
        ObservationEnvelope observation,
        string dedupKey,
        Dictionary<string, object?> context,
        string logKey,
        CancellationToken cancellationToken)
    {
        EnrichContextWithRuleMetadata(context, rule);
        var now = DateTime.UtcNow;
        var alarm = new AlarmDocument
        {
            DomainId = observation.DomainId,
            DomainName = observation.DomainName,
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

        _logger.LogInformation(
            "Alarm raised rule={RuleId} type={Type} alarm={AlarmId} key={Key}",
            rule.Id, rule.Type, alarm.Id, logKey);

        return new RuleOutcome(1, 0, 0, [alarm.Id]);
    }

    private async Task<RuleOutcome> UpdateAsync(
        AlarmDocument existing,
        Dictionary<string, object?> context,
        AlarmRuleDocument rule,
        string logKey,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        existing.LastSeenAt = now;
        existing.Count++;
        existing.Context = context;
        existing.LastPublishedAt = now;
        await _alarms.UpdateAsync(existing, cancellationToken);
        await PublishEventAsync(existing, AlarmEventTypes.Updated, context, cancellationToken);

        _logger.LogInformation(
            "Alarm updated rule={RuleId} type={Type} alarm={AlarmId} key={Key}",
            rule.Id, rule.Type, existing.Id, logKey);

        return new RuleOutcome(0, 1, 0, [existing.Id]);
    }

    private async Task<RuleOutcome> ResolveAsync(
        AlarmDocument existing,
        Dictionary<string, object?> context,
        AlarmRuleDocument rule,
        string logKey,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        existing.Status = AlarmStatus.Resolved;
        existing.LastSeenAt = now;
        existing.Context = context;
        await _alarms.UpdateAsync(existing, cancellationToken);
        await PublishEventAsync(existing, AlarmEventTypes.Resolved, context, cancellationToken);

        _logger.LogInformation(
            "Alarm resolved rule={RuleId} type={Type} alarm={AlarmId} key={Key}",
            rule.Id, rule.Type, existing.Id, logKey);

        return new RuleOutcome(0, 0, 1, [existing.Id]);
    }

    private static bool IsInCooldown(AlarmRuleDocument rule, AlarmDocument existing, DateTime now) =>
        rule.CooldownMinutes > 0
        && existing.LastPublishedAt.HasValue
        && now - existing.LastPublishedAt.Value < TimeSpan.FromMinutes(rule.CooldownMinutes);

    private static void EnrichContextWithRuleMetadata(Dictionary<string, object?> context, AlarmRuleDocument rule)
    {
        var metadata = rule.Metadata;
        if (metadata == null)
            return;

        if (!string.IsNullOrWhiteSpace(metadata.ScenarioId))
            context["scenarioId"] = metadata.ScenarioId;
        if (!string.IsNullOrWhiteSpace(metadata.PackageId))
            context["packageId"] = metadata.PackageId;
        if (!string.IsNullOrWhiteSpace(metadata.ThreatTechniqueId))
            context["threatTechniqueId"] = metadata.ThreatTechniqueId;
        if (!string.IsNullOrWhiteSpace(metadata.ThreatTechniqueName))
            context["threatTechniqueName"] = metadata.ThreatTechniqueName;
        if (!string.IsNullOrWhiteSpace(metadata.ThreatTacticId))
            context["threatTacticId"] = metadata.ThreatTacticId;
        if (metadata.ComplianceTags.Count > 0)
            context["complianceTags"] = metadata.ComplianceTags;
    }

    private static Dictionary<string, object?> BuildThresholdContext(ObservationEnvelope observation)
    {
        var ctx = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["kind"] = observation.Kind,
            ["key"] = observation.Key,
            ["value"] = observation.Value,
            ["timestamp"] = observation.Timestamp.ToString("O", CultureInfo.InvariantCulture)
        };

        foreach (var (k, v) in observation.Dimensions)
            ctx[k] = v;

        return ctx;
    }

    private Task PublishEventAsync(
        AlarmDocument alarm,
        string lifecycle,
        Dictionary<string, object?> context,
        CancellationToken cancellationToken)
    {
        return _publisher.PublishAsync(new AlarmEventMessage
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
        }, lifecycle, cancellationToken);
    }

    private readonly record struct RuleOutcome(int Raised, int Updated, int Resolved, IReadOnlyList<string> AlarmIds)
    {
        public static RuleOutcome Empty => new(0, 0, 0, Array.Empty<string>());
    }
}
