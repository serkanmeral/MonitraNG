using MngAlarm.Application.Contracts;
using MngAlarm.Application.Observations;
using MngAlarm.Application.Services;
using MngAlarm.Domain.Constants;
using MngAlarm.Domain.Entities;
using MngAlarm.Domain.Enums;
using MngAlarm.Infrastructure.Services;
using MngAlarm.Infrastructure.State;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MngAlarm.Application.Configuration;

namespace MngAlarm.Tests.Evaluation;

public sealed class SequenceProcessorTests
{
    [Fact]
    public async Task U2_failures_then_success_raises_sequence_alarm()
    {
        var rules = new FakeRuleRepository();
        var alarms = new FakeAlarmRepository();
        var publisher = new FakePublisher();
        var windows = new InMemoryCorrelationWindowStore();
        var sequences = new InMemorySequenceStateStore();

        var rule = new AlarmRuleDocument
        {
            Id = "u2",
            DomainName = "odak",
            Type = AlarmRuleTypes.Sequence,
            MatchKey = "login_success_after_failures",
            GroupByFields = ["userId", "srcIp"],
            Severity = 8,
            CooldownMinutes = 0,
            DedupKeyTemplate = "{ruleId}:{groupKey}",
            SequenceSteps =
            [
                new AlarmSequenceStep { MatchKey = "login_failed", MinCount = 3, WithinMinutes = 10 },
                new AlarmSequenceStep { MatchKey = "login_success", WithinMinutesAfterFirst = 15 }
            ]
        };
        rules.SequenceRules.Add(rule);

        var processor = new ObservationProcessor(
            rules,
            alarms,
            publisher,
            new NoOpNotificationDispatch(),
            windows,
            sequences,
            new InMemoryObservationActivityStore(),
            Options.Create(new MngAlarmSettings()),
            NullLogger<ObservationProcessor>.Instance);

        var baseTime = DateTime.UtcNow;
        var dims = new Dictionary<string, object?> { ["userId"] = "admin", ["srcIp"] = "10.1.1.1" };

        for (var i = 0; i < 3; i++)
        {
            var result = await processor.ProcessAsync(new ObservationEnvelope
            {
                DomainId = "d1",
                DomainName = "odak",
                Kind = "event",
                Key = "login_failed",
                Timestamp = baseTime.AddSeconds(i),
                Dimensions = dims
            });
            Assert.Equal(0, result.AlarmsRaised);
        }

        var raised = await processor.ProcessAsync(new ObservationEnvelope
        {
            DomainId = "d1",
            DomainName = "odak",
            Kind = "event",
            Key = "login_success",
            Timestamp = baseTime.AddSeconds(5),
            Dimensions = dims
        });

        Assert.Equal(1, raised.AlarmsRaised);
        Assert.Single(alarms.Inserted);
        Assert.Equal("login_success_after_failures", alarms.Inserted[0].Context["key"]);
        Assert.Equal("login_success", alarms.Inserted[0].Context["triggerKey"]);
        Assert.Single(publisher.Messages);
    }

    [Fact]
    public async Task U2_success_without_prior_failures_does_not_raise()
    {
        var rules = new FakeRuleRepository();
        var rule = new AlarmRuleDocument
        {
            Id = "u2",
            DomainName = "odak",
            Type = AlarmRuleTypes.Sequence,
            MatchKey = "login_success_after_failures",
            GroupByFields = ["userId"],
            SequenceSteps =
            [
                new AlarmSequenceStep { MatchKey = "login_failed", MinCount = 3, WithinMinutes = 10 },
                new AlarmSequenceStep { MatchKey = "login_success", WithinMinutesAfterFirst = 15 }
            ]
        };
        rules.SequenceRules.Add(rule);

        var processor = new ObservationProcessor(
            rules,
            new FakeAlarmRepository(),
            new FakePublisher(),
            new NoOpNotificationDispatch(),
            new InMemoryCorrelationWindowStore(),
            new InMemorySequenceStateStore(),
            new InMemoryObservationActivityStore(),
            Options.Create(new MngAlarmSettings()),
            NullLogger<ObservationProcessor>.Instance);

        var result = await processor.ProcessAsync(new ObservationEnvelope
        {
            DomainName = "odak",
            Kind = "event",
            Key = "login_success",
            Timestamp = DateTime.UtcNow,
            Dimensions = new Dictionary<string, object?> { ["userId"] = "x" }
        });

        Assert.Equal(0, result.AlarmsRaised);
    }

    private sealed class FakeRuleRepository : IAlarmRuleRepository
    {
        public List<AlarmRuleDocument> SequenceRules { get; } = [];

        public Task InsertAsync(AlarmRuleDocument rule, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<AlarmRuleDocument?> GetByIdAsync(string domainName, string ruleId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmRuleDocument?>(null);

        public Task UpdateAsync(AlarmRuleDocument rule, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(string domainName, string ruleId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<AlarmRuleDocument>> ListEnabledByKeyAsync(
            string domainName,
            string matchKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AlarmRuleDocument>>([]);

        public Task<IReadOnlyList<AlarmRuleDocument>> ListEnabledByTypeAsync(
            string domainName,
            string type,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AlarmRuleDocument>>(
                SequenceRules.Where(r => r.Enabled && string.Equals(r.Type, type, StringComparison.Ordinal)).ToList());

        public Task<IReadOnlyList<AlarmRuleDocument>> ListAllAsync(string domainName, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AlarmRuleDocument>>(SequenceRules);
    }

    private sealed class FakeAlarmRepository : IAlarmRepository
    {
        public List<AlarmDocument> Inserted { get; } = [];

        public Task InsertAsync(AlarmDocument alarm, CancellationToken cancellationToken = default)
        {
            Inserted.Add(alarm);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(AlarmDocument alarm, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<AlarmDocument?> GetActiveByDedupKeyAsync(
            string domainName,
            string dedupKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmDocument?>(null);

        public Task<AlarmDocument?> GetByIdAsync(string domainName, string alarmId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmDocument?>(null);

        public Task<IReadOnlyList<AlarmDocument>> ListActiveByRuleIdAsync(
            string domainName,
            string ruleId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AlarmDocument>>([]);

        public Task<(IReadOnlyList<AlarmDocument> Items, long Total)> ListAsync(
            string domainName,
            AlarmStatus? status,
            int? minSeverity,
            bool openOnly,
            int skip,
            int limit,
            string? ruleId = null,
            string? search = null,
            DateTime? from = null,
            DateTime? to = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<(IReadOnlyList<AlarmDocument>, long)>(([], 0));

        public Task<IReadOnlyList<AlarmScenarioRollupDto>> GetScenarioRollupAsync(
            string domainName,
            DateTime from,
            DateTime to,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AlarmScenarioRollupDto>>([]);

        public Task<IReadOnlyList<AlarmTrendBucketDto>> GetTrendBucketsAsync(
            string domainName,
            DateTime from,
            DateTime to,
            IReadOnlyList<DateTime> hourStarts,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AlarmTrendBucketDto>>([]);
    }

    private sealed class FakePublisher : IAlarmEventPublisher
    {
        public List<AlarmEventMessage> Messages { get; } = [];

        public Task PublishAsync(AlarmEventMessage message, string lifecycle, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpNotificationDispatch : IAlarmNotificationDispatchService
    {
        public Task DispatchAsync(AlarmEventMessage message, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
