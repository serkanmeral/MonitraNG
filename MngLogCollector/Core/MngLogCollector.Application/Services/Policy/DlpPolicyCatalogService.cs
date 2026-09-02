using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngLogCollector.Application.Abstractions.Policy;
using MngLogCollector.Application.Configuration;
using MngLogCollector.Application.Contracts.Policy;
using MngLogCollector.Domain.Entities;

namespace MngLogCollector.Application.Services.Policy;

public sealed class DlpPolicyCatalogService : IDlpPolicyCatalogService
{
    private readonly IDlpPolicyCatalogStore _store;
    private readonly MngLogCollectorSettings _settings;
    private readonly ILogger<DlpPolicyCatalogService> _logger;
    private readonly SemaphoreSlim _seedGate = new(1, 1);

    public DlpPolicyCatalogService(
        IDlpPolicyCatalogStore store,
        IOptions<MngLogCollectorSettings> settings,
        ILogger<DlpPolicyCatalogService> logger)
    {
        _store = store;
        _settings = settings.Value;
        _logger = logger;
    }

    private string DatabaseName
    {
        get
        {
            var name = _settings.MongoDB.EventLogCatalogDatabaseName?.Trim();
            return string.IsNullOrWhiteSpace(name) ? "mng_odak" : name;
        }
    }

    public async Task<DlpPolicyResponse> GetPublishedAsync(CancellationToken ct = default)
    {
        await EnsureSeededAsync(ct);
        var meta = await _store.GetMetaAsync(DatabaseName, ct);
        var published = await _store.GetAsync(DatabaseName, DlpPolicyDocument.PublishedId, ct);
        if (published is null)
        {
            return new DlpPolicyResponse
            {
                SchemaVersion = 1,
                PolicyId = "odak-default",
                Version = "0",
                PublishedUtc = DateTime.UnixEpoch,
                EnforcementMode = "auditOnly",
                Unclassified = new DlpUnclassifiedPolicy { Allow = true, Effect = "audit" }
            };
        }

        return ToResponse(published, meta.Version, meta.PublishedUtc);
    }

    public async Task<DlpPolicyManageResponse> GetManageAsync(CancellationToken ct = default)
    {
        await EnsureSeededAsync(ct);
        var meta = await _store.GetMetaAsync(DatabaseName, ct);
        var draft = await _store.GetAsync(DatabaseName, DlpPolicyDocument.DraftId, ct)
                    ?? FromSeed(DlpPolicySeed.CreateDefault());
        return ToManage(meta, draft);
    }

    public async Task<DlpPolicyManageResponse> UpsertDraftAsync(
        DlpPolicyUpsertRequest request,
        CancellationToken ct = default)
    {
        await EnsureSeededAsync(ct);
        var current = await _store.GetAsync(DatabaseName, DlpPolicyDocument.DraftId, ct)
                      ?? FromSeed(DlpPolicySeed.CreateDefault());
        Apply(current, request);
        Validate(current);
        current.Id = DlpPolicyDocument.DraftId;
        current.UpdatedAtUtc = DateTime.UtcNow;
        await _store.UpsertAsync(DatabaseName, current, ct);
        var meta = await _store.GetMetaAsync(DatabaseName, ct);
        return ToManage(meta, current);
    }

    public async Task<DlpPolicyResponse> PublishAsync(CancellationToken ct = default)
    {
        await EnsureSeededAsync(ct);
        var draft = await _store.GetAsync(DatabaseName, DlpPolicyDocument.DraftId, ct)
                    ?? throw new InvalidOperationException("DLP draft is missing.");
        Validate(draft);

        var version = NewVersion();
        var publishedUtc = DateTime.UtcNow;
        var published = Clone(draft);
        published.Id = DlpPolicyDocument.PublishedId;
        published.UpdatedAtUtc = publishedUtc;
        await _store.UpsertAsync(DatabaseName, published, ct);
        await _store.SaveMetaAsync(DatabaseName, new DlpCatalogMetaDocument
        {
            Version = version,
            PublishedUtc = publishedUtc
        }, ct);

        _logger.LogInformation("DLP policy published version {Version}", version);
        return ToResponse(published, version, publishedUtc);
    }

    private async Task EnsureSeededAsync(CancellationToken ct)
    {
        await _seedGate.WaitAsync(ct);
        try
        {
            var draft = await _store.GetAsync(DatabaseName, DlpPolicyDocument.DraftId, ct);
            if (draft is not null)
                return;

            var seeded = FromSeed(DlpPolicySeed.CreateDefault());
            seeded.Id = DlpPolicyDocument.DraftId;
            seeded.UpdatedAtUtc = DateTime.UtcNow;
            await _store.UpsertAsync(DatabaseName, seeded, ct);

            var published = Clone(seeded);
            published.Id = DlpPolicyDocument.PublishedId;
            await _store.UpsertAsync(DatabaseName, published, ct);

            var version = NewVersion();
            await _store.SaveMetaAsync(DatabaseName, new DlpCatalogMetaDocument
            {
                Version = version,
                PublishedUtc = DateTime.UtcNow
            }, ct);
            _logger.LogInformation("DLP policy seeded and published version {Version}", version);
        }
        finally
        {
            _seedGate.Release();
        }
    }

    private static DlpPolicyManageResponse ToManage(DlpCatalogMetaDocument meta, DlpPolicyDocument draft) =>
        new()
        {
            Version = meta.Version,
            PublishedUtc = meta.PublishedUtc,
            HasUnpublishedChanges = draft.UpdatedAtUtc > meta.PublishedUtc,
            Draft = ToResponse(draft, meta.Version, meta.PublishedUtc)
        };

    private static DlpPolicyResponse ToResponse(DlpPolicyDocument doc, string version, DateTime publishedUtc) =>
        new()
        {
            SchemaVersion = doc.SchemaVersion <= 0 ? 1 : doc.SchemaVersion,
            PolicyId = string.IsNullOrWhiteSpace(doc.PolicyId) ? "odak-default" : doc.PolicyId,
            Version = version,
            PublishedUtc = publishedUtc,
            EnforcementMode = NormalizeMode(doc.EnforcementMode),
            Unclassified = new DlpUnclassifiedPolicy
            {
                Allow = doc.Unclassified.Allow,
                Effect = NormalizeEffect(doc.Unclassified.Effect)
            },
            Classifications = doc.Classifications.Select(c => new DlpClassificationDto
            {
                Id = c.Id,
                Name = c.Name,
                Sensitivity = c.Sensitivity,
                PersistToFile = c.PersistToFile
            }).ToList(),
            Dictionaries = new DlpDictionariesDto
            {
                InternalEmailDomains = [.. doc.Dictionaries.InternalEmailDomains],
                SanctionedProcesses = [.. doc.Dictionaries.SanctionedProcesses],
                UnsanctionedProcesses = [.. doc.Dictionaries.UnsanctionedProcesses]
            },
            Rules = doc.Rules.Select(r => new DlpRuleDto
            {
                Id = r.Id,
                Name = r.Name,
                Enabled = r.Enabled,
                Priority = r.Priority,
                ClassificationIds = [.. r.ClassificationIds],
                Actions = [.. r.Actions],
                Destination = new DlpDestinationDto { EmailScope = NormalizeScope(r.EmailScope) },
                ExceptGroupIds = [.. r.ExceptGroupIds],
                Effect = NormalizeEffect(r.Effect)
            }).ToList()
        };

    private static DlpPolicyDocument FromSeed(DlpPolicyResponse seed)
    {
        var doc = new DlpPolicyDocument
        {
            SchemaVersion = seed.SchemaVersion,
            PolicyId = seed.PolicyId,
            EnforcementMode = seed.EnforcementMode,
            Unclassified = new DlpUnclassifiedState { Allow = seed.Unclassified.Allow, Effect = seed.Unclassified.Effect },
            Classifications = seed.Classifications.Select(c => new DlpClassificationState
            {
                Id = c.Id, Name = c.Name, Sensitivity = c.Sensitivity, PersistToFile = c.PersistToFile
            }).ToList(),
            Dictionaries = new DlpDictionariesState
            {
                InternalEmailDomains = [.. seed.Dictionaries.InternalEmailDomains],
                SanctionedProcesses = [.. seed.Dictionaries.SanctionedProcesses],
                UnsanctionedProcesses = [.. seed.Dictionaries.UnsanctionedProcesses]
            },
            Rules = seed.Rules.Select(r => new DlpRuleState
            {
                Id = r.Id,
                Name = r.Name,
                Enabled = r.Enabled,
                Priority = r.Priority,
                ClassificationIds = [.. r.ClassificationIds],
                Actions = [.. r.Actions],
                EmailScope = r.Destination.EmailScope,
                ExceptGroupIds = [.. r.ExceptGroupIds],
                Effect = r.Effect
            }).ToList()
        };
        return doc;
    }

    private static DlpPolicyDocument Clone(DlpPolicyDocument src) =>
        FromSeed(ToResponse(src, "0", DateTime.UnixEpoch));

    private static void Apply(DlpPolicyDocument current, DlpPolicyUpsertRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.PolicyId))
            current.PolicyId = request.PolicyId.Trim();
        if (!string.IsNullOrWhiteSpace(request.EnforcementMode))
            current.EnforcementMode = NormalizeMode(request.EnforcementMode);
        if (request.Unclassified is not null)
        {
            current.Unclassified.Allow = request.Unclassified.Allow;
            current.Unclassified.Effect = NormalizeEffect(request.Unclassified.Effect);
        }

        if (request.Classifications is not null)
        {
            current.Classifications = request.Classifications.Select(c => new DlpClassificationState
            {
                Id = (c.Id ?? string.Empty).Trim(),
                Name = (c.Name ?? string.Empty).Trim(),
                Sensitivity = c.Sensitivity,
                PersistToFile = c.PersistToFile
            }).ToList();
        }

        if (request.Dictionaries is not null)
        {
            current.Dictionaries.InternalEmailDomains = [.. request.Dictionaries.InternalEmailDomains];
            current.Dictionaries.SanctionedProcesses = [.. request.Dictionaries.SanctionedProcesses];
            current.Dictionaries.UnsanctionedProcesses = [.. request.Dictionaries.UnsanctionedProcesses];
        }

        if (request.Rules is not null)
        {
            current.Rules = request.Rules.Select(r => new DlpRuleState
            {
                Id = (r.Id ?? string.Empty).Trim(),
                Name = (r.Name ?? string.Empty).Trim(),
                Enabled = r.Enabled,
                Priority = r.Priority,
                ClassificationIds = r.ClassificationIds ?? [],
                Actions = r.Actions ?? [],
                EmailScope = NormalizeScope(r.Destination?.EmailScope),
                ExceptGroupIds = r.ExceptGroupIds ?? [],
                Effect = NormalizeEffect(r.Effect)
            }).ToList();
        }
    }

    private static void Validate(DlpPolicyDocument doc)
    {
        var priorities = new HashSet<int>();
        foreach (var rule in doc.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Id))
                throw new ArgumentException("Each DLP rule needs an id.");
            if (!priorities.Add(rule.Priority))
                throw new ArgumentException($"Duplicate DLP rule priority: {rule.Priority}.");
            if (rule.ClassificationIds.Count == 0)
                throw new ArgumentException($"Rule '{rule.Id}' needs classificationIds.");
            if (rule.Actions.Count == 0)
                throw new ArgumentException($"Rule '{rule.Id}' needs actions.");
        }
    }

    private static string NewVersion() =>
        DateTime.UtcNow.ToString("yyyy-MM-dd.HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6];

    private static string NormalizeMode(string? mode) =>
        string.Equals(mode, "enforce", StringComparison.OrdinalIgnoreCase) ? "enforce" : "auditOnly";

    private static string NormalizeEffect(string? effect)
    {
        if (string.Equals(effect, "block", StringComparison.OrdinalIgnoreCase)) return "block";
        if (string.Equals(effect, "warn", StringComparison.OrdinalIgnoreCase)) return "warn";
        return "audit";
    }

    private static string NormalizeScope(string? scope)
    {
        if (string.Equals(scope, "internal", StringComparison.OrdinalIgnoreCase)) return "internal";
        if (string.Equals(scope, "external", StringComparison.OrdinalIgnoreCase)) return "external";
        return "any";
    }
}
