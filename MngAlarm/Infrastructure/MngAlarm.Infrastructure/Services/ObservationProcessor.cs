using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngAlarm.Application.Configuration;
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
    private readonly IAlarmNotificationDispatchService _notificationDispatch;
    private readonly ICorrelationWindowStore _windows;
    private readonly ISequenceStateStore _sequences;
    private readonly IObservationActivityStore _activity;
    private readonly EngineSettings _engine;
    private readonly ILogger<ObservationProcessor> _logger;
    private readonly ScenarioGraphExecutor _graphExecutor;
    private readonly IScenarioDueStateStore _dueStates;
    private readonly TimeProvider _timeProvider;

    public ObservationProcessor(
        IAlarmRuleRepository rules,
        IAlarmRepository alarms,
        IAlarmEventPublisher publisher,
        IAlarmNotificationDispatchService notificationDispatch,
        ICorrelationWindowStore windows,
        ISequenceStateStore sequences,
        IObservationActivityStore activity,
        IOptions<MngAlarmSettings> settings,
        ILogger<ObservationProcessor> logger,
        ScenarioGraphExecutor? graphExecutor = null,
        IScenarioDueStateStore? dueStates = null,
        TimeProvider? timeProvider = null)
    {
        _rules = rules;
        _alarms = alarms;
        _publisher = publisher;
        _notificationDispatch = notificationDispatch;
        _windows = windows;
        _sequences = sequences;
        _activity = activity;
        _engine = settings.Value.Engine;
        _logger = logger;
        _graphExecutor = graphExecutor ?? new ScenarioGraphExecutor(windows, sequences);
        _dueStates = dueStates ?? new InMemoryScenarioDueStateStore();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<AlarmProcessResult> ProcessAsync(ObservationEnvelope observation, CancellationToken cancellationToken = default)
    {
        var rules = (await _rules.ListEnabledByKeyAsync(observation.DomainName, observation.Key, cancellationToken))
            .Where(x => x.Definition?.SchemaVersion != 3)
            .ToList();
        var graphRules = await _rules.ListEnabledV3CandidatesAsync(
            observation.DomainName,
            observation.Key,
            cancellationToken);
        var sequenceRules = await _rules.ListEnabledByTypeAsync(
            observation.DomainName,
            AlarmRuleTypes.Sequence,
            cancellationToken);
        var raised = 0;
        var updated = 0;
        var resolved = 0;
        var alarmIds = new List<string>();
        var rulesEvaluated = rules.Count + graphRules.Count;

        foreach (var rule in graphRules)
        {
            var outcome = await ProcessGraphAsync(rule, observation, cancellationToken);
            raised += outcome.Raised;
            updated += outcome.Updated;
            resolved += outcome.Resolved;
            alarmIds.AddRange(outcome.AlarmIds);
        }

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
                var scheduledDefinition = ScenarioCompiler.Compile(rule);
                if (ScenarioCompiler.SourceMatches(scheduledDefinition.Source, observation)
                    && StatefulScenarioConditionEvaluator.Matches(
                        rule,
                        scheduledDefinition.Condition,
                        observation,
                        _sequences))
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

    private async Task<RuleOutcome> ProcessGraphAsync(
        AlarmRuleDocument rule,
        ObservationEnvelope observation,
        CancellationToken cancellationToken)
    {
        var execution = _graphExecutor.Execute(rule, observation);
        await PersistDueStatesAsync(rule, observation, execution, null, cancellationToken);
        return await ProcessGraphOutputsAsync(rule, observation, execution, cancellationToken);
    }

    public async Task<AlarmProcessResult> ProcessDueAsync(
        ScenarioDueStateDocument state,
        CancellationToken cancellationToken = default)
    {
        var rule = await _rules.GetByIdAsync(state.DomainName, state.RuleId, cancellationToken);
        if (rule is not { Enabled: true }
            || rule.Definition?.SchemaVersion != 3
            || rule.ScenarioVersion != state.ScenarioVersion)
            return new AlarmProcessResult();

        var observation = new ObservationEnvelope
        {
            DomainId = state.DomainId,
            DomainName = state.DomainName,
            Kind = state.Observation.Kind,
            Key = state.Observation.Key,
            Value = state.Observation.Value,
            Timestamp = state.NextEvaluationAt,
            Dimensions = ObservationValueNormalizer.NormalizeDimensions(state.Observation.Dimensions)
        };
        var execution = _graphExecutor.ExecuteDue(rule, observation, state.NodeId);
        await PersistDueStatesAsync(rule, observation, execution, state.Id, cancellationToken);
        var outcome = await ProcessGraphOutputsAsync(rule, observation, execution, cancellationToken);
        return new AlarmProcessResult
        {
            RulesEvaluated = 1,
            AlarmsRaised = outcome.Raised,
            AlarmsUpdated = outcome.Updated,
            AlarmsResolved = outcome.Resolved,
            AlarmIds = outcome.AlarmIds
        };
    }

    private async Task<RuleOutcome> ProcessGraphOutputsAsync(
        AlarmRuleDocument rule,
        ObservationEnvelope observation,
        ScenarioGraphExecutionResult execution,
        CancellationToken cancellationToken)
    {
        var total = RuleOutcome.Empty;
        foreach (var output in execution.Outputs)
        {
            var existing = await _alarms.GetActiveByDedupKeyAsync(
                observation.DomainName,
                output.DedupKey,
                cancellationToken);
            var outputRule = CopyForOutput(rule, output);
            var context = BuildThresholdContext(observation);
            context["scenarioId"] = rule.ScenarioId;
            context["scenarioVersion"] = rule.ScenarioVersion;
            context["outputNodeId"] = output.OutputNodeId;
            context["groupKey"] = output.GroupKey;
            context["nextEvaluationAt"] = execution.NextEvaluationAt;
            context["nodeTrace"] = execution.Traces.Select(x => new Dictionary<string, object?>
            {
                ["nodeId"] = x.NodeId,
                ["nodeType"] = x.NodeType,
                ["status"] = x.Status,
                ["outcome"] = x.Outcome,
                ["nextEvaluationAt"] = x.NextEvaluationAt
            }).ToList();

            RuleOutcome result;
            if (existing == null)
                result = await RaiseAsync(outputRule, observation, output.DedupKey, context, output.OutputNodeId, cancellationToken);
            else if (output.CooldownSeconds > 0
                && existing.LastPublishedAt.HasValue
                && DateTime.UtcNow - existing.LastPublishedAt.Value < TimeSpan.FromSeconds(output.CooldownSeconds))
                result = RuleOutcome.Empty;
            else
                result = await UpdateAsync(existing, context, outputRule, output.OutputNodeId, cancellationToken);
            total = new RuleOutcome(
                total.Raised + result.Raised,
                total.Updated + result.Updated,
                total.Resolved + result.Resolved,
                [.. total.AlarmIds, .. result.AlarmIds]);
        }
        return total;
    }

    private async Task PersistDueStatesAsync(
        AlarmRuleDocument rule,
        ObservationEnvelope observation,
        ScenarioGraphExecutionResult execution,
        string? claimedStateId,
        CancellationToken cancellationToken)
    {
        foreach (var pending in execution.PendingEvaluations)
        {
            var id = ScenarioDueStateKeys.Create(
                observation.DomainName,
                rule.Id,
                pending.NodeId,
                pending.GroupKey);
            await _dueStates.UpsertAsync(new ScenarioDueStateDocument
            {
                Id = id,
                DomainId = observation.DomainId,
                DomainName = observation.DomainName,
                RuleId = rule.Id,
                ScenarioVersion = rule.ScenarioVersion,
                NodeId = pending.NodeId,
                NodeType = pending.NodeType,
                GroupKey = pending.GroupKey,
                NextEvaluationAt = pending.NextEvaluationAt,
                UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime,
                Observation = new ScenarioDueObservation
                {
                    Kind = observation.Kind,
                    Key = observation.Key,
                    Value = observation.Value,
                    Timestamp = observation.Timestamp,
                    Dimensions = ObservationValueNormalizer.NormalizeDimensions(observation.Dimensions)
                }
            }, cancellationToken);
        }

        foreach (var trace in execution.Traces.Where(x =>
                     x.NodeType is ScenarioNodeTypes.Threshold or ScenarioNodeTypes.Aggregation or ScenarioNodeTypes.Sequence
                     && x.Status is "true" or "false"))
        {
            var node = rule.Definition!.Graph!.Nodes.First(x => x.Id == trace.NodeId);
            var groupKey = BuildGraphGroupKey(node.Config.GroupBy, observation.Dimensions);
            var id = ScenarioDueStateKeys.Create(observation.DomainName, rule.Id, trace.NodeId, groupKey);
            if (id == claimedStateId) continue;
            await _dueStates.CancelAsync(
                observation.DomainName,
                rule.Id,
                trace.NodeId,
                groupKey,
                cancellationToken);
        }
    }

    private static string BuildGraphGroupKey(
        IReadOnlyList<string> fields,
        IReadOnlyDictionary<string, object?> dimensions) =>
        fields.Count == 0
            ? "_all"
            : string.Join("|", fields.Select(field =>
                dimensions.TryGetValue(field, out var value) ? value?.ToString() ?? "_null" : "_missing"));

    private static AlarmRuleDocument CopyForOutput(AlarmRuleDocument rule, ScenarioOutputMatch output) => new()
    {
        Id = rule.Id,
        DomainId = rule.DomainId,
        DomainName = rule.DomainName,
        Name = rule.Name,
        Type = rule.Type,
        Severity = output.Severity,
        MatchKey = rule.MatchKey,
        ScenarioId = rule.ScenarioId,
        ScenarioVersion = rule.ScenarioVersion,
        Metadata = rule.Metadata,
        CooldownMinutes = Math.Max(0, (int)Math.Ceiling(output.CooldownSeconds / 60d))
    };

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
        var now = DateTime.UtcNow;
        var definition = ScenarioCompiler.Compile(rule);
        var matches = rule.Definition == null
            ? ThresholdEvaluator.Matches(rule, observation.Value)
            : ScenarioCompiler.SourceMatches(definition.Source, observation)
                && MetaChainAllows(rule, definition, observation)
                && StatefulScenarioConditionEvaluator.Matches(rule, definition.Condition, observation, _sequences);
        if (rule.Definition != null && definition.Hysteresis != null && observation.Value.HasValue)
        {
            var hysteresis = definition.Hysteresis;
            matches = StatefulScenarioConditionEvaluator.ApplyHysteresis(
                hysteresis,
                observation.Value.Value,
                matches,
                existing?.FirstSeenAt,
                now);
        }
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
        var definition = ScenarioCompiler.Compile(rule);
        if (!CorrelationEvaluator.MatchesEvent(rule, observation)
            || !ScenarioCompiler.SourceMatches(definition.Source, observation)
            || !MetaChainAllows(rule, definition, observation)
            || !StatefulScenarioConditionEvaluator.Matches(rule, definition.Condition, observation, _sequences))
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
        var definition = ScenarioCompiler.Compile(rule);
        var steps = definition.Sequence?.Steps;
        if (steps == null || steps.Count < 2)
            return RuleOutcome.Empty;

        var groupKey = CorrelationEvaluator.BuildGroupKey(rule, observation.Dimensions);
        var storeKey = SequenceEvaluator.BuildStoreKey(observation.DomainName, rule.Id, groupKey);
        var state = _sequences.GetOrCreate(storeKey);
        var now = DateTime.UtcNow;
        var stepIndex = Math.Clamp(state.NextStepIndex, 0, steps.Count - 1);
        var step = steps[stepIndex];
        var deadlineSeconds = step.WithinSeconds > 0 ? step.WithinSeconds : Math.Max(1, rule.WindowMinutes) * 60;

        if (state.LastStepTime.HasValue
            && observation.Timestamp > state.LastStepTime.Value.AddSeconds(deadlineSeconds))
        {
            _sequences.Reset(storeKey);
            state = new SequenceRuntimeState();
            stepIndex = 0;
            step = steps[0];
        }

        if (!string.Equals(observation.Key, step.MatchKey, StringComparison.Ordinal)
            || !StatefulScenarioConditionEvaluator.Matches(
                rule,
                step.Condition,
                observation,
                _sequences,
                $"sequence.{stepIndex}"))
            return RuleOutcome.Empty;

        state.AnchorTime ??= observation.Timestamp;
        state.LastStepTime = observation.Timestamp;
        state.CurrentStepCount++;
        if (state.CurrentStepCount < Math.Max(1, step.MinCount))
        {
            _sequences.Save(storeKey, state);
            return RuleOutcome.Empty;
        }

        state.NextStepIndex = stepIndex + 1;
        state.CurrentStepCount = 0;
        state.Armed = state.NextStepIndex > 0;
        if (state.NextStepIndex < steps.Count)
        {
            _sequences.Save(storeKey, state);
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
                steps[0].MinCount);
            var updated = await UpdateAsync(existing, updateContext, rule, rule.MatchKey, cancellationToken);
            _sequences.Reset(storeKey);
            return updated;
        }

        var context = SequenceEvaluator.BuildContext(rule, observation, groupKey, state, steps[0].MinCount);
        context["sequenceStepCount"] = steps.Count;
        var raised = await RaiseAsync(rule, observation, dedupKey, context, rule.MatchKey, cancellationToken);
        _sequences.Reset(storeKey);
        return raised;
    }

    private static bool MetaChainAllows(
        AlarmRuleDocument rule,
        ScenarioDefinition definition,
        ObservationEnvelope observation)
    {
        if (definition.Source.Kind != ScenarioSourceKinds.MetaCorrelation)
            return true;
        if (!observation.Dimensions.TryGetValue("scenarioChain", out var value))
            return true;

        var chain = value switch
        {
            IEnumerable<string> strings => strings,
            string text => text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            _ => []
        };
        return string.IsNullOrWhiteSpace(rule.ScenarioId)
            || !chain.Contains(rule.ScenarioId, StringComparer.Ordinal);
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
        AlarmLifecycleHistoryHelper.AppendSystemEntry(context, AlarmStatus.Active, AlarmStatus.Active, "alarm_raised");
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
        await ProcessMetaObservationAsync(rule, observation, alarm, cancellationToken);

        _logger.LogInformation(
            "Alarm raised rule={RuleId} type={Type} alarm={AlarmId} key={Key}",
            rule.Id, rule.Type, alarm.Id, logKey);

        return new RuleOutcome(1, 0, 0, [alarm.Id]);
    }

    private async Task ProcessMetaObservationAsync(
        AlarmRuleDocument sourceRule,
        ObservationEnvelope sourceObservation,
        AlarmDocument alarm,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceRule.ScenarioId))
            return;

        var chain = sourceObservation.Dimensions.TryGetValue("scenarioChain", out var chainValue)
            ? chainValue switch
            {
                IEnumerable<string> strings => strings.ToList(),
                string text => text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                _ => []
            }
            : [];
        chain.Add(sourceRule.ScenarioId);
        if (chain.Count > 20)
            return;

        var dimensions = new Dictionary<string, object?>(alarm.Context, StringComparer.Ordinal)
        {
            ["alarmId"] = alarm.Id,
            ["sourceScenarioId"] = sourceRule.ScenarioId,
            ["scenarioChain"] = chain,
            ["scenarioChainDepth"] = chain.Count
        };
        await ProcessAsync(new ObservationEnvelope
        {
            DomainId = alarm.DomainId,
            DomainName = alarm.DomainName,
            Kind = "alarm",
            Key = AlarmEventTypes.Raised,
            Value = alarm.Severity,
            Timestamp = alarm.LastSeenAt,
            Dimensions = dimensions
        }, cancellationToken);
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
        AlarmLifecycleContextMerger.PreserveFromExisting(context, existing.Context);
        existing.Context = context;

        var publishUpdated = ShouldPublishUpdated(existing, now);
        if (publishUpdated)
            existing.LastPublishedAt = now;

        await _alarms.UpdateAsync(existing, cancellationToken);

        if (publishUpdated)
        {
            await PublishEventAsync(existing, AlarmEventTypes.Updated, context, cancellationToken);
            _logger.LogInformation(
                "Alarm updated rule={RuleId} type={Type} alarm={AlarmId} key={Key}",
                rule.Id, rule.Type, existing.Id, logKey);
        }
        else
        {
            _logger.LogDebug(
                "Alarm updated (publish suppressed) rule={RuleId} alarm={AlarmId} intervalSec={Interval}",
                rule.Id, existing.Id, _engine.UpdatedPublishMinIntervalSeconds);
        }

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
        var previousStatus = existing.Status;
        existing.Status = AlarmStatus.Resolved;
        existing.LastSeenAt = now;
        AlarmLifecycleContextMerger.PreserveFromExisting(context, existing.Context);
        existing.Context = context;
        AlarmLifecycleHistoryHelper.AppendSystemEntry(existing.Context, previousStatus, AlarmStatus.Resolved, "condition_cleared");
        await _alarms.UpdateAsync(existing, cancellationToken);
        await PublishEventAsync(existing, AlarmEventTypes.Resolved, context, cancellationToken);

        _logger.LogInformation(
            "Alarm resolved rule={RuleId} type={Type} alarm={AlarmId} key={Key}",
            rule.Id, rule.Type, existing.Id, logKey);

        return new RuleOutcome(0, 0, 1, [existing.Id]);
    }

    private bool ShouldPublishUpdated(AlarmDocument existing, DateTime now)
    {
        var interval = _engine.UpdatedPublishMinIntervalSeconds;
        if (interval <= 0)
            return true;
        if (!existing.LastPublishedAt.HasValue)
            return true;
        return now - existing.LastPublishedAt.Value >= TimeSpan.FromSeconds(interval);
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

    private readonly record struct RuleOutcome(int Raised, int Updated, int Resolved, IReadOnlyList<string> AlarmIds)
    {
        public static RuleOutcome Empty => new(0, 0, 0, Array.Empty<string>());
    }
}
