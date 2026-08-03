using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Models.SecEvents;
using MngReactor.Application.Services.SecEvents;
using MngReactor.Persistence.Services.SecEvents;
using Moq;
using Xunit;

namespace MngReactor.Tests.Services.SecEvents;

public sealed class SecEventCatalogParseEngineTests
{
    [Fact]
    public async Task TryParse_Windows4625_UsesCatalogRuleId()
    {
        var engine = CreateEngine(out _);
        using var doc = JsonDocument.Parse("""
            {"EventID":4625,"TargetUserName":"admin","IpAddress":"10.0.0.5","TimeCreated":"2026-06-03T14:00:02Z"}
            """);

        var parsed = await engine.TryParseAsync(
            "odak",
            new SecEventRawContext
            {
                ReceivedAt = DateTime.UtcNow,
                Source = new SecEventSourceInfo { Type = "ad", Product = "windows", Host = "DC01" },
                Raw = doc.RootElement.Clone()
            });

        Assert.NotNull(parsed);
        Assert.Equal("windows.logon.4625", parsed!.ParserId);
        Assert.Equal("login_failed", parsed.EventAction);
        Assert.Equal("admin", parsed.ActorUser);
        Assert.Equal("10.0.0.5", parsed.NetworkSrcIp);
    }

    [Fact]
    public async Task TryParse_LinuxFailedPassword_UsesCatalogRule()
    {
        var engine = CreateEngine(out _);
        var line = "sshd[1]: Failed password for root from 192.168.1.9";
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(line));

        var parsed = await engine.TryParseAsync(
            "odak",
            new SecEventRawContext
            {
                ReceivedAt = DateTime.UtcNow,
                Source = new SecEventSourceInfo { Type = "endpoint", Product = "linux-syslog", Host = "lx01" },
                Raw = doc.RootElement.Clone()
            });

        Assert.NotNull(parsed);
        Assert.Equal("linux.sshd.login_failed", parsed!.ParserId);
        Assert.Equal("login_failed", parsed.EventAction);
        Assert.Equal("root", parsed.ActorUser);
        Assert.Equal("192.168.1.9", parsed.NetworkSrcIp);
    }

    [Fact]
    public async Task TryParse_AgentWindowsEventLogShape_MatchesCatalog()
    {
        var engine = CreateEngine(out _);
        using var doc = JsonDocument.Parse("""
            {
              "channel": "Security",
              "package": "security-auth",
              "eventId": 4625,
              "eventData": { "TargetUserName": "alice", "IpAddress": "10.1.2.3" },
              "eventDataText": "alice | 10.1.2.3",
              "message": "alice failed logon"
            }
            """);

        var parsed = await engine.TryParseAsync(
            "odak",
            new SecEventRawContext
            {
                ReceivedAt = DateTime.UtcNow,
                Source = new SecEventSourceInfo
                {
                    Type = "windows-eventlog",
                    Product = "security-auth",
                    Host = "TERMINAL"
                },
                Raw = doc.RootElement.Clone()
            });

        Assert.NotNull(parsed);
        Assert.Equal("windows.logon.4625", parsed!.ParserId);
        Assert.Equal("alice", parsed.ActorUser);
        Assert.Equal("10.1.2.3", parsed.NetworkSrcIp);
    }

    [Fact]
    public async Task TryParse_UnmatchedFirewall_ReturnsNull_ForCodeFallback()
    {
        var engine = CreateEngine(out _);
        var line = "DENY IN=eth0 SRC=1.2.3.4 DST=10.0.0.1 DPT=445";
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(line));

        var parsed = await engine.TryParseAsync(
            "odak",
            new SecEventRawContext
            {
                ReceivedAt = DateTime.UtcNow,
                Source = new SecEventSourceInfo { Type = "firewall", Product = "generic-syslog" },
                Raw = doc.RootElement.Clone()
            });

        Assert.Null(parsed);
    }

    private static SecEventCatalogParseEngine CreateEngine(out InMemoryStore store)
    {
        store = new InMemoryStore();
        foreach (var doc in SecEventParseRuleCatalogSeed.CreateSeedDocuments())
            store.UpsertAsync("mng_odak", doc).GetAwaiter().GetResult();
        store.SaveMetaAsync("mng_odak", new SecEventParseCatalogMetaDocument
        {
            Version = "1",
            PublishedUtc = DateTime.UtcNow
        }).GetAwaiter().GetResult();

        var catalog = new Mock<ISecEventParseRuleCatalogService>();
        catalog
            .Setup(c => c.EnsureCatalogReadyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cache = new SecEventParseRuleCatalogCache(
            store,
            new Microsoft.Extensions.Caching.Memory.MemoryCache(
                new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));

        return new SecEventCatalogParseEngine(
            catalog.Object,
            cache,
            NullLogger<SecEventCatalogParseEngine>.Instance);
    }

    private sealed class InMemoryStore : ISecEventParseRuleCatalogStore
    {
        private readonly Dictionary<string, SecEventParseRuleDocument> _rules = new(StringComparer.OrdinalIgnoreCase);
        private SecEventParseCatalogMetaDocument? _meta;

        public Task EnsureIndexesAsync(string databaseName, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<SecEventParseRuleDocument>> ListAsync(string databaseName, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SecEventParseRuleDocument>>(_rules.Values.ToList());

        public Task<SecEventParseRuleDocument?> GetByRuleIdAsync(
            string databaseName,
            string ruleId,
            CancellationToken ct = default)
        {
            _rules.TryGetValue(ruleId, out var doc);
            return Task.FromResult(doc);
        }

        public Task UpsertAsync(string databaseName, SecEventParseRuleDocument doc, CancellationToken ct = default)
        {
            _rules[doc.RuleId] = doc;
            return Task.CompletedTask;
        }

        public Task<bool> DeleteByRuleIdAsync(string databaseName, string ruleId, CancellationToken ct = default) =>
            Task.FromResult(_rules.Remove(ruleId));

        public Task<SecEventParseCatalogMetaDocument?> GetMetaAsync(string databaseName, CancellationToken ct = default) =>
            Task.FromResult(_meta);

        public Task SaveMetaAsync(string databaseName, SecEventParseCatalogMetaDocument meta, CancellationToken ct = default)
        {
            _meta = meta;
            return Task.CompletedTask;
        }

        public Task<long> CountAsync(string databaseName, CancellationToken ct = default) =>
            Task.FromResult((long)_rules.Count);

        public Task<IReadOnlyList<SecEventCustomFieldDocument>> ListCustomFieldsAsync(
            string databaseName,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<SecEventCustomFieldDocument>>([]);

        public Task UpsertCustomFieldAsync(
            string databaseName,
            SecEventCustomFieldDocument doc,
            CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<bool> DeleteCustomFieldAsync(
            string databaseName,
            string name,
            CancellationToken ct = default) =>
            Task.FromResult(false);
    }
}
