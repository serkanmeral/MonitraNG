using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MngAlarm.Application.Configuration;
using MngAlarm.Application.Contracts;
using MngAlarm.Application.Services;
using MngAlarm.Domain.Entities;
using MngAlarm.Infrastructure.Services;
using MngAlarm.Infrastructure.State;

namespace MngAlarm.Tests.Evaluation;

public sealed class ScenarioDueEvaluationTests
{
    [Fact]
    public async Task Scanner_executes_due_state_without_new_observation()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new ManualTimeProvider(now);
        var store = new InMemoryScenarioDueStateStore();
        var processor = new RecordingProcessor();
        await store.UpsertAsync(State(now.AddSeconds(10)));
        var scanner = Scanner(store, processor, clock);

        Assert.Equal(0, await scanner.RunOnceAsync());
        clock.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal(1, await scanner.RunOnceAsync());
        Assert.Single(processor.Processed);
    }

    [Fact]
    public async Task Persisted_due_state_is_processed_after_scanner_restart()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new ManualTimeProvider(now);
        var store = new InMemoryScenarioDueStateStore();
        var processor = new RecordingProcessor();
        await store.UpsertAsync(State(now.AddSeconds(5)));

        Assert.Equal(0, await Scanner(store, processor, clock).RunOnceAsync());
        clock.Advance(TimeSpan.FromSeconds(5));
        var restartedScanner = Scanner(store, processor, clock);

        Assert.Equal(1, await restartedScanner.RunOnceAsync());
        Assert.Single(processor.Processed);
    }

    [Fact]
    public async Task Atomic_claim_prevents_duplicate_workers()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var store = new InMemoryScenarioDueStateStore();
        await store.UpsertAsync(State(new DateTimeOffset(now)));

        var claims = await Task.WhenAll(
            store.ClaimDueAsync(now, TimeSpan.FromSeconds(30), 10),
            store.ClaimDueAsync(now, TimeSpan.FromSeconds(30), 10));

        Assert.Equal(1, claims.Sum(x => x.Count));
        var claimed = claims.SelectMany(x => x).Single();
        Assert.False(await store.CompleteAsync(claimed.Id, "wrong-token"));
        Assert.True(await store.CompleteAsync(claimed.Id, claimed.ClaimToken!));
    }

    [Fact]
    public async Task Observation_cancellation_invalidates_an_already_returned_claim()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var store = new InMemoryScenarioDueStateStore();
        var state = State(new DateTimeOffset(now));
        await store.UpsertAsync(state);
        var claimed = (await store.ClaimDueAsync(now, TimeSpan.FromSeconds(30), 1)).Single();

        await store.CancelAsync(state.DomainName, state.RuleId, state.NodeId, state.GroupKey);

        Assert.False(await store.IsClaimValidAsync(claimed.Id, claimed.ClaimToken!));
    }

    private static ScenarioDueEvaluationService Scanner(
        IScenarioDueStateStore store,
        IObservationProcessor processor,
        TimeProvider clock)
    {
        var services = new ServiceCollection()
            .AddSingleton(processor)
            .AddSingleton<IObservationProcessor>(processor)
            .BuildServiceProvider();
        return new ScenarioDueEvaluationService(
            services.GetRequiredService<IServiceScopeFactory>(),
            store,
            clock,
            Options.Create(new MngAlarmSettings
            {
                Engine = new EngineSettings
                {
                    ScenarioDueEvaluation = new ScenarioDueEvaluationSettings
                    {
                        Enabled = true,
                        BatchSize = 10,
                        ClaimLeaseSeconds = 30
                    }
                }
            }),
            NullLogger<ScenarioDueEvaluationService>.Instance);
    }

    private static ScenarioDueStateDocument State(DateTimeOffset due) => new()
    {
        Id = ScenarioDueStateKeys.Create("tenant", "rule", "threshold", "_all"),
        DomainId = "tenant",
        DomainName = "tenant",
        RuleId = "rule",
        ScenarioVersion = 1,
        NodeId = "threshold",
        NodeType = ScenarioNodeTypes.Threshold,
        GroupKey = "_all",
        NextEvaluationAt = due.UtcDateTime,
        Observation = new ScenarioDueObservation
        {
            Kind = "event",
            Key = "burst",
            Value = 1,
            Timestamp = due.UtcDateTime.AddSeconds(-10)
        }
    };

    private sealed class RecordingProcessor : IObservationProcessor
    {
        public List<ScenarioDueStateDocument> Processed { get; } = [];

        public Task<AlarmProcessResult> ProcessAsync(
            MngAlarm.Application.Observations.ObservationEnvelope observation,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new AlarmProcessResult());

        public Task<AlarmProcessResult> ProcessDueAsync(
            ScenarioDueStateDocument state,
            CancellationToken cancellationToken = default)
        {
            Processed.Add(state);
            return Task.FromResult(new AlarmProcessResult { RulesEvaluated = 1 });
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan amount) => _now += amount;
    }
}
