using MngAlarm.Application.Contracts;
using MngAlarm.Application.Observations;
using MngAlarm.Application.Services;
using MngAlarm.Domain.Entities;
using MngAlarm.Infrastructure.Services;

namespace MngAlarm.Tests.Evaluation;

public sealed class ScenarioSchedulerTests
{
    [Fact]
    public async Task Unavailable_provider_returns_diagnostic_without_fake_success()
    {
        var scenario = ScheduledScenario();
        var service = new ScenarioSchedulerService(
            new FakeDomain(),
            new FakeRepository(scenario),
            new FakeProvider(false, []),
            new FakeProcessor());

        var result = await service.TriggerAsync(scenario.ScenarioId, 1, new ScenarioScheduleTriggerRequest());

        Assert.False(result.Supported);
        Assert.Equal("scheduled.provider.unavailable", result.DiagnosticCode);
        Assert.Equal(0, result.ObservationsProcessed);
    }

    [Fact]
    public async Task Registered_provider_processes_tenant_observations()
    {
        var scenario = ScheduledScenario();
        var processor = new FakeProcessor();
        var service = new ScenarioSchedulerService(
            new FakeDomain(),
            new FakeRepository(scenario),
            new FakeProvider(true,
            [
                new ObservationEnvelope { DomainId = "d1", DomainName = "tenant", Key = "query_result", Value = 3 }
            ]),
            processor);

        var result = await service.TriggerAsync(scenario.ScenarioId, 1, new ScenarioScheduleTriggerRequest());

        Assert.True(result.Supported);
        Assert.Equal(1, result.ObservationsProcessed);
        Assert.Equal(1, result.AlarmsRaised);
        Assert.Single(processor.Seen);
    }

    [Fact]
    public async Task Provider_cannot_cross_tenant_boundary()
    {
        var scenario = ScheduledScenario();
        var service = new ScenarioSchedulerService(
            new FakeDomain(),
            new FakeRepository(scenario),
            new FakeProvider(true,
            [
                new ObservationEnvelope { DomainName = "other", Key = "query_result" }
            ]),
            new FakeProcessor());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.TriggerAsync(scenario.ScenarioId, 1, new ScenarioScheduleTriggerRequest()));
    }

    private static ScenarioVersionDocument ScheduledScenario() => new()
    {
        ScenarioId = "scheduled-1",
        DomainId = "d1",
        DomainName = "tenant",
        Status = ScenarioLifecycleStatuses.Published,
        Enabled = true,
        Definition = new ScenarioDefinition
        {
            Source = new ScenarioSource
            {
                Kind = ScenarioSourceKinds.ScheduledQuery,
                MatchKey = "query_result",
                ScheduleDefinition = new ScenarioSchedule { Expression = "*/5 * * * *" }
            }
        }
    };

    private sealed class FakeDomain : IAlarmDomainAccessor
    {
        public AlarmDomainContext GetRequiredDomain() => new("d1", "tenant");
    }

    private sealed class FakeProvider(bool available, IReadOnlyList<ObservationEnvelope> observations) : IScenarioQueryProvider
    {
        public bool IsAvailable => available;
        public Task<IReadOnlyList<ObservationEnvelope>> QueryAsync(ScenarioQueryRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(observations);
    }

    private sealed class FakeProcessor : IObservationProcessor
    {
        public List<ObservationEnvelope> Seen { get; } = [];
        public Task<AlarmProcessResult> ProcessAsync(ObservationEnvelope observation, CancellationToken cancellationToken = default)
        {
            Seen.Add(observation);
            return Task.FromResult(new AlarmProcessResult { AlarmsRaised = 1 });
        }
    }

    private sealed class FakeRepository(ScenarioVersionDocument scenario) : IScenarioRepository
    {
        public Task<ScenarioVersionDocument?> GetVersionAsync(string domainName, string scenarioId, int version, CancellationToken cancellationToken = default) =>
            Task.FromResult<ScenarioVersionDocument?>(
                scenario.DomainName == domainName && scenario.ScenarioId == scenarioId && scenario.Version == version ? scenario : null);
        public Task InsertVersionAsync(ScenarioVersionDocument version, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateVersionAsync(ScenarioVersionDocument version, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ScenarioVersionDocument?> GetLatestAsync(string domainName, string scenarioId, CancellationToken cancellationToken = default) => Task.FromResult<ScenarioVersionDocument?>(null);
        public Task<ScenarioVersionDocument?> GetPublishedAsync(string domainName, string scenarioId, CancellationToken cancellationToken = default) => Task.FromResult<ScenarioVersionDocument?>(scenario);
        public Task<IReadOnlyList<ScenarioVersionDocument>> ListAsync(string domainName, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ScenarioVersionDocument>>([scenario]);
        public Task<IReadOnlyList<ScenarioVersionDocument>> ListVersionsAsync(string domainName, string scenarioId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ScenarioVersionDocument>>([scenario]);
        public Task ArchiveVersionAsync(string domainName, string scenarioId, int version, DateTime updatedAt, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ArchivePublishedExceptAsync(string domainName, string scenarioId, int version, DateTime updatedAt, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InsertAuditAsync(ScenarioAuditDocument audit, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<ScenarioAuditDocument>> ListAuditAsync(string domainName, string scenarioId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ScenarioAuditDocument>>([]);
    }
}
