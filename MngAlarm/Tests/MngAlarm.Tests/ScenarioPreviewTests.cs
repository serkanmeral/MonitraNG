using MngAlarm.Application.Contracts;
using MngAlarm.Application.Services;
using MngAlarm.Domain.Entities;
using MngAlarm.Infrastructure.Services;

namespace MngAlarm.Tests.Evaluation;

public sealed class ScenarioPreviewTests
{
    [Fact]
    public async Task Compile_is_side_effect_free_and_does_not_require_samples()
    {
        var service = CreateService();
        var result = await service.CompileAsync(null, null, new ScenarioPreviewRequest
        {
            Definition = BasicDefinition()
        });

        Assert.True(result.Supported);
        Assert.Empty(result.Matches);
    }

    [Fact]
    public async Task Historical_preview_returns_explicit_unsupported_diagnostic()
    {
        var service = CreateService();
        var result = await service.PreviewAsync(null, null, new ScenarioPreviewRequest
        {
            Definition = BasicDefinition(),
            From = DateTime.UtcNow.AddHours(-1),
            To = DateTime.UtcNow
        });

        Assert.False(result.Supported);
        Assert.Contains(result.Diagnostics, x => x.Code == "historical.unsupported");
    }

    [Fact]
    public async Task Simulates_three_step_sequence_per_group()
    {
        var service = CreateService();
        var definition = BasicDefinition();
        definition.GroupBy = ["user"];
        definition.Sequence = new ScenarioSequence
        {
            Steps =
            [
                new() { MatchKey = "a", MinCount = 2, WithinSeconds = 60 },
                new() { MatchKey = "b", MinCount = 1, WithinSeconds = 60 },
                new() { MatchKey = "c", MinCount = 1, WithinSeconds = 60 }
            ]
        };
        var start = DateTime.UtcNow;

        var result = await service.PreviewAsync(null, null, new ScenarioPreviewRequest
        {
            Definition = definition,
            Samples =
            [
                Sample("a", start, "u1"),
                Sample("a", start.AddSeconds(1), "u1"),
                Sample("b", start.AddSeconds(2), "u1"),
                Sample("c", start.AddSeconds(3), "u1")
            ]
        });

        Assert.Single(result.Matches, x => x.Matched);
        Assert.Single(result.DedupKeys);
        Assert.Contains("completed", result.Matches[^1].Explanation);
    }

    [Fact]
    public async Task Simulates_scheduled_staleness_without_creating_alarm()
    {
        var service = CreateService();
        var definition = BasicDefinition();
        definition.Source.Kind = ScenarioSourceKinds.ScheduledStaleness;
        definition.Window = new ScenarioWindow { DurationSeconds = 300, StalenessSeconds = 60 };
        var observed = DateTime.UtcNow.AddMinutes(-5);

        var result = await service.PreviewAsync(null, null, new ScenarioPreviewRequest
        {
            Definition = definition,
            To = DateTime.UtcNow,
            Samples = [Sample("login", observed, "u1")]
        });

        Assert.True(result.Matches.Single().Matched);
        Assert.Single(result.DedupKeys);
    }

    private static ScenarioService CreateService() =>
        new(new FakeDomain(), new FakeScenarioRepository(), new FakeRuleRepository());

    private static ScenarioDefinition BasicDefinition() => new()
    {
        Source = new ScenarioSource { MatchKey = "login" },
        Condition = new ScenarioCondition { Field = "value", Operator = "gte", Value = 1 },
        Window = new ScenarioWindow { DurationSeconds = 300 },
        Dedup = new ScenarioDedup { KeyTemplate = "{ruleId}:{groupKey}", CooldownSeconds = 60 }
    };

    private static ScenarioSampleObservation Sample(string key, DateTime timestamp, string user) => new()
    {
        Key = key,
        Value = 1,
        Timestamp = timestamp,
        Dimensions = new() { ["user"] = user }
    };

    private sealed class FakeDomain : IAlarmDomainAccessor
    {
        public AlarmDomainContext GetRequiredDomain() => new("domain-id", "test");
    }

    private sealed class FakeScenarioRepository : IScenarioRepository
    {
        public Task InsertVersionAsync(ScenarioVersionDocument version, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateVersionAsync(ScenarioVersionDocument version, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<ScenarioVersionDocument?> GetVersionAsync(string domainName, string scenarioId, int version, CancellationToken cancellationToken = default) => Task.FromResult<ScenarioVersionDocument?>(null);
        public Task<ScenarioVersionDocument?> GetLatestAsync(string domainName, string scenarioId, CancellationToken cancellationToken = default) => Task.FromResult<ScenarioVersionDocument?>(null);
        public Task<ScenarioVersionDocument?> GetPublishedAsync(string domainName, string scenarioId, CancellationToken cancellationToken = default) => Task.FromResult<ScenarioVersionDocument?>(null);
        public Task<IReadOnlyList<ScenarioVersionDocument>> ListAsync(string domainName, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ScenarioVersionDocument>>([]);
        public Task<IReadOnlyList<ScenarioVersionDocument>> ListVersionsAsync(string domainName, string scenarioId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ScenarioVersionDocument>>([]);
        public Task ArchiveVersionAsync(string domainName, string scenarioId, int version, DateTime updatedAt, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ArchivePublishedExceptAsync(string domainName, string scenarioId, int version, DateTime updatedAt, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task InsertAuditAsync(ScenarioAuditDocument audit, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<ScenarioAuditDocument>> ListAuditAsync(string domainName, string scenarioId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ScenarioAuditDocument>>([]);
        public Task UpdatePublishedEnabledAsync(string domainName, string versionId, bool enabled, DateTime updatedAt, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeRuleRepository : IAlarmRuleRepository
    {
        public Task InsertAsync(AlarmRuleDocument rule, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<AlarmRuleDocument?> GetByIdAsync(string domainName, string ruleId, CancellationToken cancellationToken = default) => Task.FromResult<AlarmRuleDocument?>(null);
        public Task UpdateAsync(AlarmRuleDocument rule, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(string domainName, string ruleId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<AlarmRuleDocument>> ListEnabledByKeyAsync(string domainName, string matchKey, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AlarmRuleDocument>>([]);
        public Task<IReadOnlyList<AlarmRuleDocument>> ListEnabledByTypeAsync(string domainName, string type, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AlarmRuleDocument>>([]);
        public Task<IReadOnlyList<AlarmRuleDocument>> ListAllAsync(string domainName, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AlarmRuleDocument>>([]);
    }
}
