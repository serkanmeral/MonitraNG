using MngAlarm.Application.Contracts;
using MngAlarm.Application.Services;
using MngAlarm.Domain.Entities;
using MngAlarm.Infrastructure.Services;

namespace MngAlarm.Tests;

public sealed class AlarmRuleMetadataTests
{
    [Fact]
    public async Task CreateAsync_PersistsPackageMetadata()
    {
        AlarmRuleDocument? inserted = null;
        var rules = new FakeRuleRepository(doc => inserted = doc);
        var domain = new FakeDomainAccessor("dom-id", "odak");
        var sut = new AlarmRuleService(domain, rules);

        await sut.CreateAsync(new CreateAlarmRuleRequest
        {
            Name = "U1 pack",
            Type = "correlation",
            MatchKey = "login_failed",
            Threshold = 10,
            Severity = 7,
            Metadata = new AlarmRuleMetadataDto
            {
                PackageId = "siem-mvp-v1",
                PackageVersion = "1.0.0",
                ScenarioId = "U1",
                ThreatTechniqueId = "T1110.001",
                ThreatTacticId = "TA0006",
                ComplianceTags = ["ISO27001:A.8.5"]
            }
        });

        Assert.NotNull(inserted);
        Assert.NotNull(inserted!.Metadata);
        Assert.Equal("siem-mvp-v1", inserted.Metadata!.PackageId);
        Assert.Equal("U1", inserted.Metadata.ScenarioId);
        Assert.Equal("T1110.001", inserted.Metadata.ThreatTechniqueId);
        Assert.Single(inserted.Metadata.ComplianceTags);
    }

    private sealed class FakeDomainAccessor(string domainId, string domainName) : IAlarmDomainAccessor
    {
        public AlarmDomainContext GetRequiredDomain() => new(domainId, domainName);
    }

    private sealed class FakeRuleRepository(Action<AlarmRuleDocument> onInsert) : IAlarmRuleRepository
    {
        public Task InsertAsync(AlarmRuleDocument rule, CancellationToken cancellationToken = default)
        {
            onInsert(rule);
            return Task.CompletedTask;
        }

        public Task<AlarmRuleDocument?> GetByIdAsync(string domainName, string ruleId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AlarmRuleDocument?>(null);

        public Task UpdateAsync(AlarmRuleDocument rule, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeleteAsync(string domainName, string ruleId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<AlarmRuleDocument>> ListEnabledByKeyAsync(string domainName, string matchKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AlarmRuleDocument>>([]);

        public Task<IReadOnlyList<AlarmRuleDocument>> ListEnabledByTypeAsync(string domainName, string type, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AlarmRuleDocument>>([]);

        public Task<IReadOnlyList<AlarmRuleDocument>> ListAllAsync(string domainName, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AlarmRuleDocument>>([]);
    }
}
