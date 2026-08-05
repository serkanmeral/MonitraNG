using MngAlarm.Application.Contracts;
using MngAlarm.Application.Services;
using MngAlarm.Domain.Entities;
using MngAlarm.Infrastructure.Services;

namespace MngAlarm.Tests.Evaluation;

public sealed class ScenarioLifecycleTests
{
    [Fact]
    public async Task V3_draft_validate_publish_creates_graph_projection()
    {
        var versions = new MemoryScenarioRepository();
        var rules = new MemoryRuleRepository();
        var service = new ScenarioService(new FakeDomain(), versions, rules);
        var draft = await service.CreateDraftAsync(new CreateScenarioDraftRequest
        {
            Name = "V3",
            Enabled = true,
            Definition = V3Definition()
        });

        var validation = await service.ValidateAsync(draft.ScenarioId, draft.Version);
        var published = await service.PublishAsync(draft.ScenarioId, draft.Version);

        Assert.True(validation!.IsValid);
        Assert.Equal(ScenarioLifecycleStatuses.Published, published!.Status);
        Assert.Equal(3, rules.Items.Single().Definition!.SchemaVersion);
        Assert.Equal("login.v3", rules.Items.Single().MatchKey);
        Assert.Equal(9, rules.Items.Single().Severity);
    }

    [Fact]
    public async Task Draft_validate_publish_is_immutable_and_creates_legacy_projection()
    {
        var versions = new MemoryScenarioRepository();
        var rules = new MemoryRuleRepository();
        var service = new ScenarioService(new FakeDomain(), versions, rules);
        var draft = await service.CreateDraftAsync(new CreateScenarioDraftRequest
        {
            Name = "Login",
            Enabled = true,
            Severity = 8,
            Definition = Definition()
        });

        var validation = await service.ValidateAsync(draft.ScenarioId, 1);
        var published = await service.PublishAsync(draft.ScenarioId, 1);
        var forbiddenEdit = await service.UpdateDraftAsync(
            draft.ScenarioId,
            1,
            new UpdateScenarioDraftRequest { Name = "mutated" });

        Assert.True(validation?.IsValid);
        Assert.Equal(ScenarioLifecycleStatuses.Published, published?.Status);
        Assert.Null(forbiddenEdit);
        Assert.Single(rules.Items);
        Assert.Equal(draft.ScenarioId, rules.Items[0].ScenarioId);
        Assert.Equal("login", rules.Items[0].MatchKey);
        Assert.Equal(1, rules.Items[0].ScenarioVersion);
    }

    [Fact]
    public async Task Rollback_creates_new_editable_version_and_archive_disables_projection()
    {
        var versions = new MemoryScenarioRepository();
        var rules = new MemoryRuleRepository();
        var service = new ScenarioService(new FakeDomain(), versions, rules);
        var draft = await service.CreateDraftAsync(new CreateScenarioDraftRequest
        {
            Name = "Login",
            Enabled = true,
            Definition = Definition()
        });
        await service.ValidateAsync(draft.ScenarioId, 1);
        await service.PublishAsync(draft.ScenarioId, 1);

        var rollback = await service.RollbackAsync(draft.ScenarioId, 1);
        var archived = await service.ArchiveAsync(draft.ScenarioId, 1);

        Assert.Equal(2, rollback?.Version);
        Assert.Equal(ScenarioLifecycleStatuses.Draft, rollback?.Status);
        Assert.Equal(ScenarioLifecycleStatuses.Archived, archived?.Status);
        Assert.False(rules.Items.Single().Enabled);
        Assert.Contains(versions.Audit, x => x.Action == "version.archived");
    }

    [Fact]
    public async Task Publishing_new_version_archives_previous_and_keeps_single_projection()
    {
        var versions = new MemoryScenarioRepository();
        var rules = new MemoryRuleRepository();
        var service = new ScenarioService(new FakeDomain(), versions, rules);
        var first = await service.CreateDraftAsync(new CreateScenarioDraftRequest
        {
            Name = "v1",
            Enabled = true,
            Definition = Definition()
        });
        await service.ValidateAsync(first.ScenarioId, 1);
        await service.PublishAsync(first.ScenarioId, 1);
        var second = await service.CreateNextDraftAsync(first.ScenarioId, new CreateScenarioDraftRequest
        {
            Name = "v2",
            Enabled = true,
            Definition = Definition()
        });
        await service.ValidateAsync(first.ScenarioId, second!.Version);
        await service.PublishAsync(first.ScenarioId, second.Version);
        var rollback = await service.RollbackAsync(first.ScenarioId, 1);

        Assert.Equal(ScenarioLifecycleStatuses.Archived, versions.Items.Single(x => x.Version == 1).Status);
        Assert.Equal(ScenarioLifecycleStatuses.Published, versions.Items.Single(x => x.Version == 2).Status);
        Assert.Equal(3, rollback!.Version);
        Assert.Equal(ScenarioLifecycleStatuses.Draft, rollback.Status);
        Assert.Single(rules.Items);
        Assert.Equal(2, rules.Items[0].ScenarioVersion);
    }

    [Fact]
    public async Task Product_template_is_read_only_and_clone_is_user_draft()
    {
        var versions = new MemoryScenarioRepository();
        var service = new ScenarioService(new FakeDomain(), versions, new MemoryRuleRepository());
        var imported = await service.ImportProductPackageAsync(new ImportScenarioPackageRequest
        {
            PackageId = "siem",
            PackageVersion = "2.0.0",
            Templates = [new() { TemplateId = "U1", Name = "U1", Definition = Definition() }]
        });
        var templateId = imported.ScenarioIds.Single();
        var secondImport = await service.ImportProductPackageAsync(new ImportScenarioPackageRequest
        {
            PackageId = "siem",
            PackageVersion = "2.0.0",
            Templates = [new() { TemplateId = "U1", Name = "U1", Definition = Definition() }]
        });
        var forbidden = await service.UpdateDraftAsync(templateId, 1, new UpdateScenarioDraftRequest { Name = "x" });
        var forbiddenNext = await service.CreateNextDraftAsync(templateId, null);
        var clone = await service.CloneTemplateAsync(templateId, 1);

        Assert.Null(forbidden);
        Assert.Null(forbiddenNext);
        Assert.Equal(1, secondImport.Skipped);
        Assert.NotNull(clone);
        Assert.Equal(ScenarioOrigins.User, clone!.Origin);
        Assert.False(clone.IsReadOnly);
        Assert.Equal("U1", clone.TemplateId);
        Assert.NotEqual(templateId, clone.ScenarioId);
    }

    [Fact]
    public async Task Product_import_normalizes_graph_condition_json_values_before_persistence()
    {
        var versions = new MemoryScenarioRepository();
        var service = new ScenarioService(new FakeDomain(), versions, new MemoryRuleRepository());
        var jsonValue = System.Text.Json.JsonDocument.Parse("5").RootElement.Clone();
        var definition = new ScenarioDefinition
        {
            SchemaVersion = 3,
            Graph = new ScenarioGraph
            {
                Nodes =
                [
                    new()
                    {
                        Id = "source",
                        Type = ScenarioNodeTypes.Source,
                        Config = new() { Source = new() { MatchKey = "login_failed" } }
                    },
                    new()
                    {
                        Id = "decision",
                        Type = ScenarioNodeTypes.Decision,
                        Config = new()
                        {
                            Condition = new()
                            {
                                Field = "value",
                                Operator = "gte",
                                Value = jsonValue
                            }
                        }
                    },
                    new()
                    {
                        Id = "alarm",
                        Type = ScenarioNodeTypes.AlarmOutput,
                        Config = new()
                        {
                            Severity = 7,
                            Dedup = new() { KeyTemplate = "{scenarioId}:{outputNodeId}" }
                        }
                    }
                ],
                Edges =
                [
                    new() { Id = "e1", From = "source", To = "decision", FromPort = "next" },
                    new() { Id = "e2", From = "decision", To = "alarm", FromPort = "true" }
                ]
            }
        };

        await service.ImportProductPackageAsync(new ImportScenarioPackageRequest
        {
            PackageId = "siem-v3",
            PackageVersion = "3.0.0",
            Templates = [new() { TemplateId = "U1", Name = "U1", Definition = definition }]
        });

        var value = versions.Items.Single().Definition.Graph!.Nodes
            .Single(x => x.Id == "decision").Config.Condition!.Value;
        Assert.IsNotType<System.Text.Json.JsonElement>(value);
        Assert.Equal(5d, Convert.ToDouble(value));
    }

    [Fact]
    public async Task Catalog_is_tenant_scoped_and_summarizes_versions()
    {
        var versions = new MemoryScenarioRepository();
        versions.Items.Add(new ScenarioVersionDocument { DomainName = "other", ScenarioId = "foreign", Name = "foreign" });
        var service = new ScenarioService(new FakeDomain(), versions, new MemoryRuleRepository());
        var draft = await service.CreateDraftAsync(new CreateScenarioDraftRequest { Name = "local", Definition = Definition() });
        await service.ValidateAsync(draft.ScenarioId, 1);
        await service.PublishAsync(draft.ScenarioId, 1);
        await service.CreateNextDraftAsync(draft.ScenarioId, null);

        var catalog = await service.ListAsync(true);

        Assert.Single(catalog);
        Assert.Equal(2, catalog[0].LatestVersion);
        Assert.Equal(1, catalog[0].PublishedVersion);
        Assert.Equal(2, catalog[0].DraftVersion);
    }

    [Fact]
    public async Task Scheduled_publish_is_capability_gated()
    {
        var versions = new MemoryScenarioRepository();
        var service = new ScenarioService(
            new FakeDomain(),
            versions,
            new MemoryRuleRepository(),
            new FakeCapabilities(false, true));
        var definition = Definition();
        definition.Source.Kind = ScenarioSourceKinds.ScheduledQuery;
        definition.Source.ScheduleDefinition = new ScenarioSchedule { Expression = "*/5 * * * *" };
        var draft = await service.CreateDraftAsync(new CreateScenarioDraftRequest
        {
            Name = "scheduled",
            Enabled = true,
            Definition = definition
        });

        var validation = await service.ValidateAsync(draft.ScenarioId, 1);

        Assert.False(validation!.IsValid);
        Assert.Contains(validation.Diagnostics, x => x.Code == "scheduled.provider.unavailable");
    }

    [Fact]
    public async Task Scheduled_publish_is_allowed_when_provider_capability_exists()
    {
        var service = new ScenarioService(
            new FakeDomain(),
            new MemoryScenarioRepository(),
            new MemoryRuleRepository(),
            new FakeCapabilities(true, true));
        var definition = Definition();
        definition.Source.Kind = ScenarioSourceKinds.ScheduledQuery;
        definition.Source.ScheduleDefinition = new ScenarioSchedule { Expression = "*/5 * * * *" };
        var draft = await service.CreateDraftAsync(new CreateScenarioDraftRequest
        {
            Name = "scheduled",
            Enabled = true,
            Definition = definition
        });

        var validation = await service.ValidateAsync(draft.ScenarioId, 1);
        var published = await service.PublishAsync(draft.ScenarioId, 1);

        Assert.True(validation!.IsValid);
        Assert.Equal(ScenarioLifecycleStatuses.Published, published!.Status);
    }

    [Fact]
    public async Task Meta_graph_cycle_blocks_validation()
    {
        var versions = new MemoryScenarioRepository();
        var service = new ScenarioService(
            new FakeDomain(),
            versions,
            new MemoryRuleRepository(),
            new FakeCapabilities(true, true));
        var firstDefinition = Definition();
        firstDefinition.Source.Kind = ScenarioSourceKinds.MetaCorrelation;
        firstDefinition.Source.MaxChainDepth = 5;
        firstDefinition.Source.DependsOnScenarioIds = ["placeholder"];
        var first = await service.CreateDraftAsync(new CreateScenarioDraftRequest { Name = "A", Enabled = true, Definition = firstDefinition });
        var secondDefinition = Definition();
        secondDefinition.Source.Kind = ScenarioSourceKinds.MetaCorrelation;
        secondDefinition.Source.MaxChainDepth = 5;
        secondDefinition.Source.DependsOnScenarioIds = [first.ScenarioId];
        var second = await service.CreateDraftAsync(new CreateScenarioDraftRequest { Name = "B", Enabled = true, Definition = secondDefinition });
        firstDefinition.Source.DependsOnScenarioIds = [second.ScenarioId];
        await service.UpdateDraftAsync(first.ScenarioId, 1, new UpdateScenarioDraftRequest { Definition = firstDefinition });

        var validation = await service.ValidateAsync(first.ScenarioId, 1);

        Assert.False(validation!.IsValid);
        Assert.Contains(validation.Diagnostics, x => x.Code == "meta.graph.cycle_or_depth");
    }

    [Fact]
    public async Task Scheduled_staleness_validates_and_projects_to_validation_scan_rule()
    {
        var versions = new MemoryScenarioRepository();
        var rules = new MemoryRuleRepository();
        var service = new ScenarioService(new FakeDomain(), versions, rules);
        var definition = Definition();
        definition.Source.Kind = ScenarioSourceKinds.ScheduledStaleness;
        definition.Window = new ScenarioWindow { DurationSeconds = 300, StalenessSeconds = 600 };
        var draft = await service.CreateDraftAsync(new CreateScenarioDraftRequest
        {
            Name = "stale",
            Enabled = true,
            Definition = definition
        });

        var validation = await service.ValidateAsync(draft.ScenarioId, 1);
        await service.PublishAsync(draft.ScenarioId, 1);

        Assert.True(validation!.IsValid);
        Assert.Equal("scheduled", rules.Items.Single().Type);
        Assert.Equal(10, rules.Items.Single().StalenessMinutes);
    }

    private static ScenarioDefinition Definition() => new()
    {
        Source = new ScenarioSource { MatchKey = "login" },
        Condition = new ScenarioCondition { Field = "value", Operator = "gte", Value = 1 },
        Window = new ScenarioWindow { DurationSeconds = 300 },
        Dedup = new ScenarioDedup { KeyTemplate = "{ruleId}:{key}", CooldownSeconds = 60 }
    };

    private static ScenarioDefinition V3Definition() => new()
    {
        SchemaVersion = 3,
        Graph = new ScenarioGraph
        {
            Nodes =
            [
                new() { Id = "source", Type = ScenarioNodeTypes.Source, Config = new() { Source = new() { MatchKey = "login.v3" } } },
                new() { Id = "output", Type = ScenarioNodeTypes.AlarmOutput, Config = new() { Severity = 9, Dedup = new() { KeyTemplate = "{scenarioId}:{outputNodeId}", CooldownSeconds = 60 } } }
            ],
            Edges = [new() { Id = "edge", From = "source", To = "output", FromPort = "next" }]
        }
    };

    private sealed class FakeDomain : IAlarmDomainAccessor
    {
        public AlarmDomainContext GetRequiredDomain() => new("domain-id", "test");
    }

    private sealed record FakeCapabilities(
        bool ScheduledQueryAvailable,
        bool MetaCorrelationAvailable) : IScenarioRuntimeCapabilities;

    private sealed class MemoryScenarioRepository : IScenarioRepository
    {
        public List<ScenarioVersionDocument> Items { get; } = [];
        public List<ScenarioAuditDocument> Audit { get; } = [];

        public Task InsertVersionAsync(ScenarioVersionDocument version, CancellationToken cancellationToken = default)
        {
            Items.Add(version);
            return Task.CompletedTask;
        }

        public Task UpdateVersionAsync(ScenarioVersionDocument version, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<ScenarioVersionDocument?> GetVersionAsync(string domainName, string scenarioId, int version, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.DomainName == domainName && x.ScenarioId == scenarioId && x.Version == version));

        public Task<ScenarioVersionDocument?> GetLatestAsync(string domainName, string scenarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.Where(x => x.DomainName == domainName && x.ScenarioId == scenarioId).OrderByDescending(x => x.Version).FirstOrDefault());

        public Task<ScenarioVersionDocument?> GetPublishedAsync(string domainName, string scenarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.Where(x => x.DomainName == domainName && x.ScenarioId == scenarioId && x.Status == ScenarioLifecycleStatuses.Published).OrderByDescending(x => x.Version).FirstOrDefault());

        public Task<IReadOnlyList<ScenarioVersionDocument>> ListAsync(string domainName, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ScenarioVersionDocument>>(Items.Where(x => x.DomainName == domainName).ToList());

        public Task<IReadOnlyList<ScenarioVersionDocument>> ListVersionsAsync(string domainName, string scenarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ScenarioVersionDocument>>(Items.Where(x => x.ScenarioId == scenarioId).ToList());

        public Task ArchiveVersionAsync(string domainName, string scenarioId, int version, DateTime updatedAt, CancellationToken cancellationToken = default)
        {
            var item = Items.Single(x => x.ScenarioId == scenarioId && x.Version == version);
            item.Status = ScenarioLifecycleStatuses.Archived;
            item.UpdatedAt = updatedAt;
            return Task.CompletedTask;
        }

        public Task ArchivePublishedExceptAsync(string domainName, string scenarioId, int version, DateTime updatedAt, CancellationToken cancellationToken = default)
        {
            foreach (var item in Items.Where(x => x.DomainName == domainName
                && x.ScenarioId == scenarioId
                && x.Version != version
                && x.Status == ScenarioLifecycleStatuses.Published))
            {
                item.Status = ScenarioLifecycleStatuses.Archived;
                item.UpdatedAt = updatedAt;
            }
            return Task.CompletedTask;
        }

        public Task InsertAuditAsync(ScenarioAuditDocument audit, CancellationToken cancellationToken = default)
        {
            Audit.Add(audit);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ScenarioAuditDocument>> ListAuditAsync(string domainName, string scenarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ScenarioAuditDocument>>(Audit.Where(x => x.ScenarioId == scenarioId).ToList());
    }

    private sealed class MemoryRuleRepository : IAlarmRuleRepository
    {
        public List<AlarmRuleDocument> Items { get; } = [];
        public Task InsertAsync(AlarmRuleDocument rule, CancellationToken cancellationToken = default)
        {
            Items.Add(rule);
            return Task.CompletedTask;
        }
        public Task<AlarmRuleDocument?> GetByIdAsync(string domainName, string ruleId, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(x => x.Id == ruleId));
        public Task UpdateAsync(AlarmRuleDocument rule, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(string domainName, string ruleId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<AlarmRuleDocument>> ListEnabledByKeyAsync(string domainName, string matchKey, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AlarmRuleDocument>>(Items.Where(x => x.Enabled && x.MatchKey == matchKey).ToList());
        public Task<IReadOnlyList<AlarmRuleDocument>> ListEnabledByTypeAsync(string domainName, string type, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AlarmRuleDocument>>(Items.Where(x => x.Enabled && x.Type == type).ToList());
        public Task<IReadOnlyList<AlarmRuleDocument>> ListAllAsync(string domainName, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AlarmRuleDocument>>(Items);
    }
}
