using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MngLogCollector.Application.Abstractions.Policy;
using MngLogCollector.Application.Configuration;
using MngLogCollector.Application.Contracts.Policy;
using MngLogCollector.Application.Services.Policy;
using MngLogCollector.Domain.Entities;

namespace MngLogCollector.Tests;

public class DlpPolicyCatalogServiceTests
{
    [Fact]
    public async Task Seed_publishes_default_and_etag_version()
    {
        var svc = Create();
        var published = await svc.GetPublishedAsync();
        Assert.Equal("odak-default", published.PolicyId);
        Assert.Equal("auditOnly", published.EnforcementMode);
        Assert.Contains(published.Rules, r => r.Id == "r-gizli-email-external-block");
        Assert.NotEqual("0", published.Version);
    }

    [Fact]
    public async Task Draft_update_is_not_visible_until_publish()
    {
        var svc = Create();
        var before = await svc.GetPublishedAsync();
        await svc.UpsertDraftAsync(new DlpPolicyUpsertRequest
        {
            EnforcementMode = "enforce",
            Rules =
            [
                new DlpRuleDto
                {
                    Id = "r-only",
                    Name = "only",
                    Enabled = true,
                    Priority = 10,
                    ClassificationIds = ["*"],
                    Actions = ["email.send"],
                    Destination = new DlpDestinationDto { EmailScope = "any" },
                    Effect = "audit"
                }
            ]
        });

        var still = await svc.GetPublishedAsync();
        Assert.Equal(before.Version, still.Version);
        Assert.Equal("auditOnly", still.EnforcementMode);

        var published = await svc.PublishAsync();
        Assert.Equal("enforce", published.EnforcementMode);
        Assert.NotEqual(before.Version, published.Version);
        Assert.Single(published.Rules);
    }

    private static DlpPolicyCatalogService Create()
    {
        var settings = Options.Create(new MngLogCollectorSettings
        {
            MongoDB = new MongoDbSettings { EventLogCatalogDatabaseName = "test_dlp" }
        });
        return new DlpPolicyCatalogService(new MemoryStore(), settings, NullLogger<DlpPolicyCatalogService>.Instance);
    }

    private sealed class MemoryStore : IDlpPolicyCatalogStore
    {
        private readonly Dictionary<string, DlpPolicyDocument> _docs = new(StringComparer.Ordinal);
        private DlpCatalogMetaDocument _meta = new();

        public Task EnsureIndexesAsync(string databaseName, CancellationToken ct = default) => Task.CompletedTask;

        public Task<DlpPolicyDocument?> GetAsync(string databaseName, string id, CancellationToken ct = default)
        {
            _docs.TryGetValue(id, out var doc);
            return Task.FromResult(doc);
        }

        public Task UpsertAsync(string databaseName, DlpPolicyDocument doc, CancellationToken ct = default)
        {
            _docs[doc.Id] = doc;
            return Task.CompletedTask;
        }

        public Task<DlpCatalogMetaDocument> GetMetaAsync(string databaseName, CancellationToken ct = default) =>
            Task.FromResult(_meta);

        public Task SaveMetaAsync(string databaseName, DlpCatalogMetaDocument meta, CancellationToken ct = default)
        {
            _meta = meta;
            return Task.CompletedTask;
        }
    }
}
