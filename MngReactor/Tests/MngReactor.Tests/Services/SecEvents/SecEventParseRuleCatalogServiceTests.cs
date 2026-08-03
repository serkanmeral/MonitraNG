using Microsoft.Extensions.Logging.Abstractions;
using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Contracts.SecEvents;
using MngReactor.Application.Models.SecEvents;
using MngReactor.Application.Services.SecEvents;
using Xunit;

namespace MngReactor.Tests.Services.SecEvents;

public sealed class SecEventParseRuleCatalogServiceTests
{
    private const string Domain = "odak";

    [Fact]
    public async Task Create_Publish_And_Preview_CustomRule()
    {
        var sut = CreateSut(out var store);

        var request = ValidWindows4625();
        request.RuleId = "custom.windows.logon.test";
        var created = await sut.CreateAsync(Domain, request);
        Assert.Equal("custom.windows.logon.test", created.RuleId);
        Assert.False(created.Builtin);

        var managed = await sut.ListManagedAsync(Domain);
        Assert.True(managed.HasUnpublishedChanges);
        Assert.Contains(managed.Items, i => i.RuleId == "custom.windows.logon.test");
        Assert.True(managed.Items.Count >= SecEventParseRuleCatalogSeed.CreateSeedDocuments().Count);

        var published = await sut.PublishAsync(Domain);
        Assert.NotEqual("0", published.Version);
        Assert.Contains(published.Rules, r => r.RuleId == "custom.windows.logon.test");

        managed = await sut.ListManagedAsync(Domain);
        Assert.False(managed.HasUnpublishedChanges);

        var preview = await sut.PreviewAsync(Domain, new SecEventParseRulePreviewRequest
        {
            RuleId = "custom.windows.logon.test",
            Context = new SecEventParseRulePreviewContext
            {
                Source = new SecEventParseRulePreviewSource { Product = "windows", Type = "ad" },
                EventId = 4625,
                Channel = "Security",
                Raw = new
                {
                    EventID = 4625,
                    EventData = new { TargetUserName = "admin", IpAddress = "10.0.0.5" }
                }
            }
        });

        Assert.True(preview.Matched);
        Assert.Equal("login_failed", preview.Fields["event.action"]?.ToString());
        Assert.Equal("admin", preview.Fields["actor.user"]?.ToString());
        Assert.Equal("10.0.0.5", preview.Fields["network.srcIp"]?.ToString());
        Assert.True(await store.CountAsync($"mng_{Domain}") >= 1);
    }

    [Fact]
    public async Task EnsureCatalogReady_SeedsBuiltinRules()
    {
        var sut = CreateSut(out var store);
        await sut.EnsureCatalogReadyAsync(Domain);
        var count = await store.CountAsync($"mng_{Domain}");
        Assert.Equal(SecEventParseRuleCatalogSeed.CreateSeedDocuments().Count, count);
    }

    [Fact]
    public async Task Create_RejectsInvalidTarget_BeforeStore()
    {
        var sut = CreateSut(out _);
        var request = ValidWindows4625();
        request.Extract.Add(new SecEventParseRuleExtractStepDto
        {
            Type = "constant",
            To = "threat.technique.id",
            Value = "T1110"
        });

        await Assert.ThrowsAsync<ArgumentException>(() => sut.CreateAsync(Domain, request));
    }

    [Fact]
    public async Task Delete_Builtin_Fails()
    {
        var sut = CreateSut(out var store);
        await sut.ListManagedAsync(Domain); // seed meta

        await store.UpsertAsync($"mng_{Domain}", new SecEventParseRuleDocument
        {
            RuleId = "windows.logon.4624",
            Name = "builtin sample",
            Builtin = true,
            Enabled = true,
            Match = new SecEventParseRuleMatch { SourceProduct = ["windows"] },
            Extract =
            [
                new SecEventParseRuleExtractStep
                {
                    Type = "constant",
                    To = "event.action",
                    Value = "login_success"
                }
            ]
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.DeleteAsync(Domain, "windows.logon.4624"));
        Assert.Contains("Builtin", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static SecEventParseRuleCatalogService CreateSut(out InMemoryParseRuleStore store)
    {
        store = new InMemoryParseRuleStore();
        var cache = new MockCache();
        return new SecEventParseRuleCatalogService(
            store,
            cache,
            NullLogger<SecEventParseRuleCatalogService>.Instance);
    }

    private sealed class MockCache : ISecEventParseRuleCatalogCache
    {
        public Task<IReadOnlyList<SecEventParseRuleDocument>> GetEnabledRulesAsync(
            string domain,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SecEventParseRuleDocument>>([]);

        public void Invalidate(string domain)
        {
        }
    }

    private static SecEventParseRuleUpsertRequest ValidWindows4625() => new()
    {
        RuleId = "windows.logon.4625",
        Name = "Windows failed logon",
        Enabled = true,
        Priority = 100,
        Match = new SecEventParseRuleMatchDto
        {
            SourceProduct = ["windows"],
            SourceType = ["ad"],
            Channel = ["Security"],
            EventIds = [4625]
        },
        Extract =
        [
            new SecEventParseRuleExtractStepDto
            {
                Type = "event_data",
                From = "TargetUserName",
                To = "actor.user"
            },
            new SecEventParseRuleExtractStepDto
            {
                Type = "event_data",
                From = "IpAddress",
                To = "network.srcIp"
            },
            new SecEventParseRuleExtractStepDto
            {
                Type = "constant",
                To = "event.action",
                Value = "login_failed"
            },
            new SecEventParseRuleExtractStepDto
            {
                Type = "constant",
                To = "event.outcome",
                Value = "failure"
            }
        ]
    };

    private sealed class InMemoryParseRuleStore : ISecEventParseRuleCatalogStore
    {
        private readonly Dictionary<string, Dictionary<string, SecEventParseRuleDocument>> _rules = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SecEventParseCatalogMetaDocument> _meta = new(StringComparer.OrdinalIgnoreCase);

        public Task EnsureIndexesAsync(string databaseName, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<SecEventParseRuleDocument>> ListAsync(string databaseName, CancellationToken ct = default)
        {
            if (!_rules.TryGetValue(databaseName, out var map))
                return Task.FromResult<IReadOnlyList<SecEventParseRuleDocument>>([]);
            return Task.FromResult<IReadOnlyList<SecEventParseRuleDocument>>(map.Values.Select(Clone).ToList());
        }

        public Task<SecEventParseRuleDocument?> GetByRuleIdAsync(
            string databaseName,
            string ruleId,
            CancellationToken ct = default)
        {
            if (_rules.TryGetValue(databaseName, out var map) &&
                map.TryGetValue(ruleId.Trim().ToLowerInvariant(), out var doc))
                return Task.FromResult<SecEventParseRuleDocument?>(Clone(doc));
            return Task.FromResult<SecEventParseRuleDocument?>(null);
        }

        public Task UpsertAsync(string databaseName, SecEventParseRuleDocument doc, CancellationToken ct = default)
        {
            if (!_rules.TryGetValue(databaseName, out var map))
            {
                map = new Dictionary<string, SecEventParseRuleDocument>(StringComparer.OrdinalIgnoreCase);
                _rules[databaseName] = map;
            }

            map[doc.RuleId.Trim().ToLowerInvariant()] = Clone(doc);
            return Task.CompletedTask;
        }

        public Task<bool> DeleteByRuleIdAsync(string databaseName, string ruleId, CancellationToken ct = default)
        {
            if (!_rules.TryGetValue(databaseName, out var map))
                return Task.FromResult(false);
            return Task.FromResult(map.Remove(ruleId.Trim().ToLowerInvariant()));
        }

        public Task<SecEventParseCatalogMetaDocument?> GetMetaAsync(string databaseName, CancellationToken ct = default)
        {
            _meta.TryGetValue(databaseName, out var meta);
            return Task.FromResult(meta is null ? null : new SecEventParseCatalogMetaDocument
            {
                Id = meta.Id,
                Version = meta.Version,
                PublishedUtc = meta.PublishedUtc,
                BuiltinSeedRevision = meta.BuiltinSeedRevision
            });
        }

        public Task SaveMetaAsync(string databaseName, SecEventParseCatalogMetaDocument meta, CancellationToken ct = default)
        {
            _meta[databaseName] = new SecEventParseCatalogMetaDocument
            {
                Id = SecEventParseCatalogMetaDocument.SingletonId,
                Version = meta.Version,
                PublishedUtc = meta.PublishedUtc,
                BuiltinSeedRevision = meta.BuiltinSeedRevision
            };
            return Task.CompletedTask;
        }

        public Task<long> CountAsync(string databaseName, CancellationToken ct = default)
        {
            if (!_rules.TryGetValue(databaseName, out var map))
                return Task.FromResult(0L);
            return Task.FromResult((long)map.Count);
        }

        private readonly Dictionary<string, Dictionary<string, SecEventCustomFieldDocument>> _custom =
            new(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<SecEventCustomFieldDocument>> ListCustomFieldsAsync(
            string databaseName,
            CancellationToken ct = default)
        {
            if (!_custom.TryGetValue(databaseName, out var map))
                return Task.FromResult<IReadOnlyList<SecEventCustomFieldDocument>>([]);
            return Task.FromResult<IReadOnlyList<SecEventCustomFieldDocument>>(map.Values.ToList());
        }

        public Task UpsertCustomFieldAsync(
            string databaseName,
            SecEventCustomFieldDocument doc,
            CancellationToken ct = default)
        {
            if (!_custom.TryGetValue(databaseName, out var map))
            {
                map = new Dictionary<string, SecEventCustomFieldDocument>(StringComparer.Ordinal);
                _custom[databaseName] = map;
            }

            map[doc.Name] = doc;
            return Task.CompletedTask;
        }

        public Task<bool> DeleteCustomFieldAsync(
            string databaseName,
            string name,
            CancellationToken ct = default)
        {
            if (!_custom.TryGetValue(databaseName, out var map))
                return Task.FromResult(false);
            return Task.FromResult(map.Remove(name));
        }

        private static SecEventParseRuleDocument Clone(SecEventParseRuleDocument d) => new()
        {
            Id = d.Id,
            RuleId = d.RuleId,
            Name = d.Name,
            Description = d.Description,
            Enabled = d.Enabled,
            Priority = d.Priority,
            Builtin = d.Builtin,
            Version = d.Version,
            Match = d.Match,
            Extract = d.Extract.ToList(),
            OnConflict = d.OnConflict,
            CreatedAtUtc = d.CreatedAtUtc,
            UpdatedAtUtc = d.UpdatedAtUtc
        };
    }
}
