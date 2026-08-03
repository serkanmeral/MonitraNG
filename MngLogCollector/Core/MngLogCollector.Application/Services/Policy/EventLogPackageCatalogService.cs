using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MngLogCollector.Application.Abstractions.Policy;
using MngLogCollector.Application.Configuration;
using MngLogCollector.Application.Contracts.Policy;
using MngLogCollector.Domain.Entities;

namespace MngLogCollector.Application.Services.Policy;

public sealed class EventLogPackageCatalogService : IEventLogPackageCatalogService
{
    private readonly IEventLogPackageCatalogStore _store;
    private readonly MngLogCollectorSettings _settings;
    private readonly ILogger<EventLogPackageCatalogService> _logger;
    private readonly SemaphoreSlim _seedGate = new(1, 1);

    public EventLogPackageCatalogService(
        IEventLogPackageCatalogStore store,
        IOptions<MngLogCollectorSettings> settings,
        ILogger<EventLogPackageCatalogService> logger)
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

    public async Task<EventLogPackageCatalogResponse> GetCatalogAsync(
        string? hostname = null,
        CancellationToken ct = default)
    {
        await EnsureSeededAsync(ct);
        var meta = await _store.GetMetaAsync(DatabaseName, ct);
        var docs = await _store.ListAsync(DatabaseName, ct);

        var hostKey = NormalizeHostKey(hostname);
        EventLogHostAssignmentDocument? assignment = null;
        if (!string.IsNullOrEmpty(hostKey))
            assignment = await _store.GetAssignmentAsync(DatabaseName, hostKey, ct);

        return ToAgentCatalog(meta, docs, assignment, hostKey);
    }

    public async Task<EventLogPackageManageListResponse> ListManagedAsync(CancellationToken ct = default)
    {
        await EnsureSeededAsync(ct);
        var meta = await _store.GetMetaAsync(DatabaseName, ct);
        var docs = await _store.ListAsync(DatabaseName, ct);
        var maxUpdated = docs.Count == 0
            ? (DateTime?)null
            : docs.Max(x => x.UpdatedAtUtc);
        var unpublished = maxUpdated.HasValue && maxUpdated.Value > meta.PublishedUtc;

        return new EventLogPackageManageListResponse
        {
            Version = meta.Version,
            PublishedUtc = meta.PublishedUtc,
            HasUnpublishedChanges = unpublished,
            Items = docs.Select(ToManageItem).ToList()
        };
    }

    public async Task<EventLogPackageManageItemDto> CreateAsync(
        EventLogPackageUpsertRequest request,
        CancellationToken ct = default)
    {
        await EnsureSeededAsync(ct);
        Validate(request, isCreate: true);
        var name = NormalizeName(request.Name);
        if (await _store.GetByNameAsync(DatabaseName, name, ct) is not null)
            throw new InvalidOperationException($"Package '{name}' already exists.");

        var now = DateTime.UtcNow;
        var mode = NormalizeSelectionMode(request.SelectionMode);
        var doc = new EventLogPackageDocument
        {
            Name = name,
            Channel = request.Channel.Trim(),
            SelectionMode = mode,
            EventIds = mode == "all" ? [] : NormalizeIds(request.EventIds),
            ExcludedEventIds = mode == "all" ? NormalizeIds(request.ExcludedEventIds ?? []) : [],
            IsDefault = request.IsDefault,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        await _store.UpsertAsync(DatabaseName, doc, ct);
        return ToManageItem(doc);
    }

    public async Task<EventLogPackageManageItemDto> UpdateAsync(
        string name,
        EventLogPackageUpsertRequest request,
        CancellationToken ct = default)
    {
        await EnsureSeededAsync(ct);
        var key = NormalizeName(name);
        var existing = await _store.GetByNameAsync(DatabaseName, key, ct)
            ?? throw new InvalidOperationException($"Package '{key}' not found.");

        Validate(request, isCreate: false);
        var newName = string.IsNullOrWhiteSpace(request.Name) ? key : NormalizeName(request.Name);
        if (!string.Equals(newName, key, StringComparison.Ordinal) &&
            await _store.GetByNameAsync(DatabaseName, newName, ct) is not null)
        {
            throw new InvalidOperationException($"Package '{newName}' already exists.");
        }

        if (!string.Equals(newName, key, StringComparison.Ordinal))
            await _store.DeleteByNameAsync(DatabaseName, key, ct);

        var mode = NormalizeSelectionMode(request.SelectionMode);
        existing.Name = newName;
        existing.Channel = request.Channel.Trim();
        existing.SelectionMode = mode;
        existing.EventIds = mode == "all" ? [] : NormalizeIds(request.EventIds);
        existing.ExcludedEventIds = mode == "all" ? NormalizeIds(request.ExcludedEventIds ?? []) : [];
        existing.IsDefault = request.IsDefault;
        existing.UpdatedAtUtc = DateTime.UtcNow;
        await _store.UpsertAsync(DatabaseName, existing, ct);
        return ToManageItem(existing);
    }

    public async Task DeleteAsync(string name, CancellationToken ct = default)
    {
        await EnsureSeededAsync(ct);
        var key = NormalizeName(name);
        if (!await _store.DeleteByNameAsync(DatabaseName, key, ct))
            throw new InvalidOperationException($"Package '{key}' not found.");
    }

    public async Task<EventLogPackageCatalogResponse> PublishAsync(CancellationToken ct = default)
    {
        await EnsureSeededAsync(ct);
        var version = DateTime.UtcNow.ToString("yyyy-MM-dd.HHmmss");
        var meta = new EventLogCatalogMetaDocument
        {
            Version = version,
            PublishedUtc = DateTime.UtcNow
        };
        await _store.SaveMetaAsync(DatabaseName, meta, ct);
        _logger.LogInformation("Event Log package catalog published version={Version}", version);

        var docs = await _store.ListAsync(DatabaseName, ct);
        return ToAgentCatalog(meta, docs, assignment: null, hostKey: null);
    }

    public async Task<EventLogHostAssignmentDto> GetAssignmentAsync(string hostname, CancellationToken ct = default)
    {
        await EnsureSeededAsync(ct);
        var hostKey = NormalizeHostKey(hostname);
        if (string.IsNullOrEmpty(hostKey))
            throw new ArgumentException("Hostname is required.");

        var doc = await _store.GetAssignmentAsync(DatabaseName, hostKey, ct);
        return ToAssignmentDto(hostname, hostKey, doc);
    }

    public async Task<EventLogHostAssignmentDto> UpsertAssignmentAsync(
        string hostname,
        EventLogHostAssignmentUpsertRequest request,
        CancellationToken ct = default)
    {
        await EnsureSeededAsync(ct);
        var hostKey = NormalizeHostKey(hostname);
        if (string.IsNullOrEmpty(hostKey))
            throw new ArgumentException("Hostname is required.");

        request ??= new EventLogHostAssignmentUpsertRequest();
        var docs = await _store.ListAsync(DatabaseName, ct);
        var optionalNames = docs.Where(d => !d.IsDefault).Select(d => d.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var enabled = NormalizePackageNames(request.EnabledOptionalPackages)
            .Where(n => optionalNames.Contains(n))
            .ToList();
        // Fleet defaults are never disabled via host assignment (SIEM host modal).
        var disabled = new List<string>();

        var doc = new EventLogHostAssignmentDocument
        {
            HostKey = hostKey,
            EnabledOptionalPackages = enabled,
            DisabledServerPackages = disabled,
            UpdatedAtUtc = DateTime.UtcNow
        };

        if (enabled.Count == 0)
        {
            await _store.DeleteAssignmentAsync(DatabaseName, hostKey, ct);
            _logger.LogInformation("Cleared Event Log host assignment hostKey={HostKey}", hostKey);
            return ToAssignmentDto(hostname, hostKey, null);
        }

        await _store.UpsertAssignmentAsync(DatabaseName, doc, ct);
        _logger.LogInformation(
            "Saved Event Log host assignment hostKey={HostKey} enabled={Enabled}",
            hostKey,
            enabled.Count);
        return ToAssignmentDto(hostname, hostKey, doc);
    }

    public async Task DeleteAssignmentAsync(string hostname, CancellationToken ct = default)
    {
        await EnsureSeededAsync(ct);
        var hostKey = NormalizeHostKey(hostname);
        if (string.IsNullOrEmpty(hostKey))
            throw new ArgumentException("Hostname is required.");
        await _store.DeleteAssignmentAsync(DatabaseName, hostKey, ct);
    }

    public IReadOnlyList<EventLogChannelDictionaryDto> GetChannelDictionary() =>
        EventLogChannelDictionary.All;

    public IReadOnlyList<EventLogPackagePresetDto> GetPresets() =>
        EventLogPackagePresets.All;

    private async Task EnsureSeededAsync(CancellationToken ct)
    {
        var count = await _store.CountAsync(DatabaseName, ct);
        if (count > 0)
            return;

        await _seedGate.WaitAsync(ct);
        try
        {
            count = await _store.CountAsync(DatabaseName, ct);
            if (count > 0)
                return;

            _logger.LogInformation(
                "Seeding Event Log package catalog into {Database}",
                DatabaseName);

            foreach (var doc in EventLogPackageCatalogSeed.CreateSeedDocuments())
                await _store.UpsertAsync(DatabaseName, doc, ct);

            await _store.SaveMetaAsync(DatabaseName, new EventLogCatalogMetaDocument
            {
                Version = EventLogPackageCatalogSeed.InitialVersion,
                PublishedUtc = DateTime.UtcNow
            }, ct);
        }
        finally
        {
            _seedGate.Release();
        }
    }

    private static EventLogPackageCatalogResponse ToAgentCatalog(
        EventLogCatalogMetaDocument meta,
        IReadOnlyList<EventLogPackageDocument> docs,
        EventLogHostAssignmentDocument? assignment,
        string? hostKey)
    {
        // Legacy DisabledServerPackages on host assignments are ignored — fleet defaults
        // apply to every host; only optional enablement is host-scoped.
        var enabledOptional = new HashSet<string>(
            assignment?.EnabledOptionalPackages ?? [],
            StringComparer.OrdinalIgnoreCase);

        var packages = docs
            .Where(x => x.IsDefault)
            .Select(d => ToDto(d, isDefault: true))
            .ToList();

        foreach (var opt in docs.Where(x => !x.IsDefault && enabledOptional.Contains(x.Name)))
            packages.Add(ToDto(opt, isDefault: false));

        packages = packages.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();

        // Assigned optionals are already in packages; keep them out of optionalPackages
        // so agents / Local UI do not show the same name twice.
        var optional = docs
            .Where(x => !x.IsDefault && !enabledOptional.Contains(x.Name))
            .Select(d => ToDto(d, isDefault: false))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var assignmentStamp = assignment is null
            ? "0"
            : assignment.UpdatedAtUtc.Ticks.ToString();
        var version = string.IsNullOrEmpty(hostKey)
            ? meta.Version
            : $"{meta.Version}+{hostKey}+{assignmentStamp}";

        return new EventLogPackageCatalogResponse
        {
            Version = version,
            Source = "collector",
            GeneratedUtc = DateTime.UtcNow,
            Packages = packages,
            OptionalPackages = optional
        };
    }

    private static EventLogHostAssignmentDto ToAssignmentDto(
        string hostname,
        string hostKey,
        EventLogHostAssignmentDocument? doc) => new()
    {
        Hostname = hostname?.Trim() ?? hostKey,
        HostKey = hostKey,
        EnabledOptionalPackages = doc?.EnabledOptionalPackages?.ToList() ?? [],
        DisabledServerPackages = doc?.DisabledServerPackages?.ToList() ?? [],
        UpdatedAtUtc = doc?.UpdatedAtUtc
    };

    private static EventLogPackageDto ToDto(EventLogPackageDocument d, bool isDefault)
    {
        var mode = ResolveSelectionMode(d);
        return new EventLogPackageDto
        {
            Name = d.Name,
            Channel = d.Channel,
            SelectionMode = mode,
            EventIds = mode == "all" ? [] : [.. (d.EventIds ?? [])],
            ExcludedEventIds = mode == "all" ? [.. (d.ExcludedEventIds ?? [])] : [],
            IsDefault = isDefault
        };
    }

    private static EventLogPackageDto ToDto(EventLogPackageDocument d) => ToDto(d, d.IsDefault);

    private static EventLogPackageManageItemDto ToManageItem(EventLogPackageDocument d)
    {
        var mode = ResolveSelectionMode(d);
        return new EventLogPackageManageItemDto
        {
            Id = d.Id,
            Name = d.Name,
            Channel = d.Channel,
            SelectionMode = mode,
            EventIds = mode == "all" ? [] : [.. (d.EventIds ?? [])],
            ExcludedEventIds = mode == "all" ? [.. (d.ExcludedEventIds ?? [])] : [],
            IsDefault = d.IsDefault,
            UpdatedAtUtc = d.UpdatedAtUtc
        };
    }

    private static void Validate(EventLogPackageUpsertRequest request, bool isCreate)
    {
        if (isCreate && string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Name is required.");
        if (string.IsNullOrWhiteSpace(request.Channel))
            throw new ArgumentException("Channel is required.");

        var mode = NormalizeSelectionMode(request.SelectionMode);
        if (mode == "selected" && (request.EventIds is null || NormalizeIds(request.EventIds).Count == 0))
            throw new ArgumentException("At least one Event ID is required when selectionMode is selected.");
    }

    /// <summary>Legacy docs without SelectionMode behave as selected.</summary>
    private static string ResolveSelectionMode(EventLogPackageDocument d) =>
        NormalizeSelectionMode(d.SelectionMode);

    private static string NormalizeSelectionMode(string? mode) =>
        string.Equals(mode?.Trim(), "all", StringComparison.OrdinalIgnoreCase) ? "all" : "selected";

    private static List<int> NormalizeIds(IEnumerable<int>? ids) =>
        (ids ?? []).Where(x => x > 0).Distinct().OrderBy(x => x).ToList();

    private static string NormalizeName(string name) =>
        (name ?? string.Empty).Trim().ToLowerInvariant();

    private static string NormalizeHostKey(string? hostname) =>
        (hostname ?? string.Empty).Trim().ToLowerInvariant().Split('.')[0];

    private static List<string> NormalizePackageNames(IEnumerable<string>? names) =>
        (names ?? [])
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
