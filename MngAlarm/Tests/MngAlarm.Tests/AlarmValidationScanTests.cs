using Microsoft.Extensions.Logging.Abstractions;
using MngAlarm.Application.Contracts;
using MngAlarm.Application.Observations;
using MngAlarm.Application.Services;
using MngAlarm.Domain.Constants;
using MngAlarm.Domain.Entities;
using MngAlarm.Domain.Enums;
using MngAlarm.Infrastructure.Evaluation;
using MngAlarm.Infrastructure.Services;
using MngAlarm.Infrastructure.State;

namespace MngAlarm.Tests.Evaluation;

public sealed class AlarmValidationScanTests
{
    [Fact]
    public async Task Correlation_scan_resolves_open_alarm_below_window_threshold()
    {
        var rule = new AlarmRuleDocument
        {
            Id = "r1",
            DomainName = "tenant",
            Type = AlarmRuleTypes.Correlation,
            Threshold = 2,
            WindowMinutes = 5
        };
        var alarm = new AlarmDocument
        {
            Id = "a1",
            DomainName = "tenant",
            RuleId = rule.Id,
            Status = AlarmStatus.Active,
            Context = new() { ["groupKey"] = "user-1" }
        };
        var windows = new InMemoryCorrelationWindowStore();
        windows.RecordAndCount(
            CorrelationEvaluator.BuildStoreKey("tenant", rule.Id, "user-1"),
            DateTime.UtcNow,
            TimeSpan.FromMinutes(5));
        var alarms = new FakeAlarmRepository(alarm);
        var publisher = new FakePublisher();
        var service = new AlarmValidationService(
            new FakeRuleRepository(rule),
            alarms,
            publisher,
            new NoOpDispatch(),
            windows,
            new InMemoryObservationActivityStore(),
            NullLogger<AlarmValidationService>.Instance);

        var result = await service.RunScanAsync("tenant", "d1");

        Assert.Equal(1, result.CorrelationResolved);
        Assert.Equal(AlarmStatus.Resolved, alarm.Status);
        Assert.Single(publisher.Messages);
    }

    private sealed class FakeRuleRepository(AlarmRuleDocument rule) : IAlarmRuleRepository
    {
        public Task<IReadOnlyList<AlarmRuleDocument>> ListEnabledByTypeAsync(string domainName, string type, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AlarmRuleDocument>>(type == AlarmRuleTypes.Correlation ? [rule] : []);
        public Task InsertAsync(AlarmRuleDocument item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<AlarmRuleDocument?> GetByIdAsync(string domainName, string ruleId, CancellationToken cancellationToken = default) => Task.FromResult<AlarmRuleDocument?>(rule);
        public Task UpdateAsync(AlarmRuleDocument item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(string domainName, string ruleId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<AlarmRuleDocument>> ListEnabledByKeyAsync(string domainName, string matchKey, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AlarmRuleDocument>>([]);
        public Task<IReadOnlyList<AlarmRuleDocument>> ListAllAsync(string domainName, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AlarmRuleDocument>>([rule]);
    }

    private sealed class FakeAlarmRepository(AlarmDocument alarm) : IAlarmRepository
    {
        public Task<IReadOnlyList<AlarmDocument>> ListActiveByRuleIdAsync(string domainName, string ruleId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AlarmDocument>>([alarm]);
        public Task UpdateAsync(AlarmDocument item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<AlarmDocument?> GetActiveByDedupKeyAsync(string domainName, string dedupKey, CancellationToken cancellationToken = default) => Task.FromResult<AlarmDocument?>(null);
        public Task<AlarmDocument?> GetByIdAsync(string domainName, string alarmId, CancellationToken cancellationToken = default) => Task.FromResult<AlarmDocument?>(alarm);
        public Task InsertAsync(AlarmDocument item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<(IReadOnlyList<AlarmDocument> Items, long Total)> ListAsync(string domainName, AlarmStatus? status, int? minSeverity, bool openOnly, int skip, int limit, string? ruleId = null, string? search = null, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default) => Task.FromResult<(IReadOnlyList<AlarmDocument>, long)>(([alarm], 1));
        public Task<IReadOnlyList<AlarmScenarioRollupDto>> GetScenarioRollupAsync(string domainName, DateTime from, DateTime to, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AlarmScenarioRollupDto>>([]);
        public Task<IReadOnlyList<AlarmTrendBucketDto>> GetTrendBucketsAsync(string domainName, DateTime from, DateTime to, IReadOnlyList<DateTime> hourStarts, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AlarmTrendBucketDto>>([]);
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

    private sealed class NoOpDispatch : IAlarmNotificationDispatchService
    {
        public Task DispatchAsync(AlarmEventMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
