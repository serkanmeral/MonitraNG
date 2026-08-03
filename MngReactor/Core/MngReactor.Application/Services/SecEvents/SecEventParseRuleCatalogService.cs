using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MngReactor.Application.Abstractions.SecEvents;
using MngReactor.Application.Contracts.SecEvents;
using MngReactor.Application.Models.SecEvents;

namespace MngReactor.Application.Services.SecEvents;

public sealed class SecEventParseRuleCatalogService : ISecEventParseRuleCatalogService
{
    private readonly ISecEventParseRuleCatalogStore _store;
    private readonly ISecEventParseRuleCatalogCache _cache;
    private readonly ILogger<SecEventParseRuleCatalogService> _logger;
    private readonly SemaphoreSlim _seedGate = new(1, 1);

    public SecEventParseRuleCatalogService(
        ISecEventParseRuleCatalogStore store,
        ISecEventParseRuleCatalogCache cache,
        ILogger<SecEventParseRuleCatalogService> logger)
    {
        _store = store;
        _cache = cache;
        _logger = logger;
    }

    public Task EnsureCatalogReadyAsync(string domain, CancellationToken ct = default) =>
        EnsureSeededAsync(Db(domain), ct);

    public async Task<SecEventParseRuleManageListResponse> ListManagedAsync(
        string domain,
        CancellationToken ct = default)
    {
        var db = Db(domain);
        await EnsureSeededAsync(db, ct);
        var meta = await _store.GetMetaAsync(db, ct) ?? new SecEventParseCatalogMetaDocument();
        var docs = await _store.ListAsync(db, ct);
        var maxUpdated = docs.Count == 0 ? (DateTime?)null : docs.Max(x => x.UpdatedAtUtc);
        var unpublished = maxUpdated.HasValue && maxUpdated.Value > meta.PublishedUtc;

        return new SecEventParseRuleManageListResponse
        {
            Version = meta.Version,
            PublishedUtc = meta.PublishedUtc,
            HasUnpublishedChanges = unpublished,
            Items = docs
                .OrderByDescending(x => x.Priority)
                .ThenBy(x => x.RuleId, StringComparer.Ordinal)
                .Select(ToManageItem)
                .ToList()
        };
    }

    public async Task<SecEventParseRuleManageItemDto?> GetManagedAsync(
        string domain,
        string ruleId,
        CancellationToken ct = default)
    {
        var db = Db(domain);
        await EnsureSeededAsync(db, ct);
        var doc = await _store.GetByRuleIdAsync(db, SecEventParseRuleValidator.NormalizeRuleId(ruleId), ct);
        return doc is null ? null : ToManageItem(doc);
    }

    public async Task<SecEventParseRuleManageItemDto> CreateAsync(
        string domain,
        SecEventParseRuleUpsertRequest request,
        CancellationToken ct = default)
    {
        var db = Db(domain);
        await EnsureSeededAsync(db, ct);
        SecEventParseRuleValidator.ValidateUpsert(request, isCreate: true);

        var ruleId = SecEventParseRuleValidator.NormalizeRuleId(request.RuleId);
        if (await _store.GetByRuleIdAsync(db, ruleId, ct) is not null)
            throw new InvalidOperationException($"Parse rule '{ruleId}' already exists.");

        var now = DateTime.UtcNow;
        var doc = FromUpsert(request, ruleId, builtin: false, version: 1, createdAt: now, existingMongoId: null);
        await _store.UpsertAsync(db, doc, ct);
        await RegisterCustomTargetsAsync(db, request.Extract, ct);
        _cache.Invalidate(domain);
        return ToManageItem(doc);
    }

    public async Task<SecEventParseRuleManageItemDto> UpdateAsync(
        string domain,
        string ruleId,
        SecEventParseRuleUpsertRequest request,
        CancellationToken ct = default)
    {
        var db = Db(domain);
        await EnsureSeededAsync(db, ct);
        var key = SecEventParseRuleValidator.NormalizeRuleId(ruleId);
        var existing = await _store.GetByRuleIdAsync(db, key, ct)
            ?? throw new InvalidOperationException($"Parse rule '{key}' not found.");

        SecEventParseRuleValidator.ValidateUpsert(request, isCreate: false);
        var newId = string.IsNullOrWhiteSpace(request.RuleId)
            ? key
            : SecEventParseRuleValidator.NormalizeRuleId(request.RuleId);

        if (!string.Equals(newId, key, StringComparison.Ordinal))
        {
            if (existing.Builtin)
                throw new InvalidOperationException("Builtin parse rule id cannot be renamed.");
            if (await _store.GetByRuleIdAsync(db, newId, ct) is not null)
                throw new InvalidOperationException($"Parse rule '{newId}' already exists.");
            await _store.DeleteByRuleIdAsync(db, key, ct);
        }

        var doc = FromUpsert(
            request,
            newId,
            builtin: existing.Builtin,
            version: existing.Version + 1,
            createdAt: existing.CreatedAtUtc,
            existingMongoId: existing.Id);
        await _store.UpsertAsync(db, doc, ct);
        await RegisterCustomTargetsAsync(db, request.Extract, ct);
        _cache.Invalidate(domain);
        return ToManageItem(doc);
    }

    public async Task DeleteAsync(string domain, string ruleId, CancellationToken ct = default)
    {
        var db = Db(domain);
        await EnsureSeededAsync(db, ct);
        var key = SecEventParseRuleValidator.NormalizeRuleId(ruleId);
        var existing = await _store.GetByRuleIdAsync(db, key, ct)
            ?? throw new InvalidOperationException($"Parse rule '{key}' not found.");
        if (existing.Builtin)
            throw new InvalidOperationException("Builtin parse rules cannot be deleted; disable them instead.");

        if (!await _store.DeleteByRuleIdAsync(db, key, ct))
            throw new InvalidOperationException($"Parse rule '{key}' not found.");
        _cache.Invalidate(domain);
    }

    public async Task<SecEventParseRulePublishedResponse> PublishAsync(
        string domain,
        CancellationToken ct = default)
    {
        var db = Db(domain);
        await EnsureSeededAsync(db, ct);
        var existingMeta = await _store.GetMetaAsync(db, ct);
        var version = DateTime.UtcNow.ToString("yyyy-MM-dd.HHmmss");
        var meta = new SecEventParseCatalogMetaDocument
        {
            Version = version,
            PublishedUtc = DateTime.UtcNow,
            BuiltinSeedRevision = existingMeta?.BuiltinSeedRevision
                                  ?? SecEventParseRuleCatalogSeed.SeedRevision
        };
        await _store.SaveMetaAsync(db, meta, ct);
        _cache.Invalidate(domain);
        _logger.LogInformation(
            "Sec-event parse rule catalog published domain={Domain} version={Version}",
            domain,
            version);

        return await BuildPublishedAsync(db, meta, ct);
    }

    public async Task<SecEventParseRulePublishedResponse> GetPublishedAsync(
        string domain,
        CancellationToken ct = default)
    {
        var db = Db(domain);
        await EnsureSeededAsync(db, ct);
        var meta = await _store.GetMetaAsync(db, ct) ?? new SecEventParseCatalogMetaDocument();
        return await BuildPublishedAsync(db, meta, ct);
    }

    public async Task<SecEventParseRulePreviewResponse> PreviewAsync(
        string domain,
        SecEventParseRulePreviewRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var db = Db(domain);
        await EnsureSeededAsync(db, ct);

        SecEventParseRuleDocument? rule = null;
        var isDraft = request.DraftRule is not null;
        if (isDraft)
        {
            SecEventParseRuleValidator.ValidateUpsert(request.DraftRule!, isCreate: true);
            var draftId = SecEventParseRuleValidator.NormalizeRuleId(request.DraftRule!.RuleId);
            rule = FromUpsert(
                request.DraftRule,
                draftId,
                builtin: false,
                version: 0,
                createdAt: DateTime.UtcNow,
                existingMongoId: null);
        }
        else
        {
            var docs = (await _store.ListAsync(db, ct))
                .Where(d => d.Enabled)
                .OrderByDescending(d => d.Priority)
                .ThenBy(d => d.RuleId, StringComparer.Ordinal)
                .ToList();

            if (!string.IsNullOrWhiteSpace(request.RuleId))
            {
                var id = SecEventParseRuleValidator.NormalizeRuleId(request.RuleId);
                rule = docs.FirstOrDefault(d => d.RuleId == id)
                    ?? await _store.GetByRuleIdAsync(db, id, ct);
                if (rule is null)
                    throw new InvalidOperationException($"Parse rule '{id}' not found.");
            }
            else
            {
                rule = docs.FirstOrDefault(d => Matches(d, request.Context));
            }
        }

        var response = new SecEventParseRulePreviewResponse();
        if (rule is null)
        {
            response.Matched = false;
            response.Notes.Add("No matching parse rule.");
            return response;
        }

        if (!Matches(rule, request.Context))
        {
            response.Matched = false;
            response.RuleId = rule.RuleId;
            response.Notes.Add(isDraft
                ? "Draft rule match conditions were not satisfied for this sample."
                : "Rule found but match conditions were not satisfied.");
            return response;
        }

        response.Matched = true;
        response.RuleId = rule.RuleId;
        if (isDraft)
            response.Notes.Add("Preview used unsaved draft rule.");
        ApplyExtract(rule, request.Context, response);
        return response;
    }

    private async Task<SecEventParseRulePublishedResponse> BuildPublishedAsync(
        string db,
        SecEventParseCatalogMetaDocument meta,
        CancellationToken ct)
    {
        var docs = await _store.ListAsync(db, ct);
        return new SecEventParseRulePublishedResponse
        {
            Version = meta.Version,
            PublishedUtc = meta.PublishedUtc,
            Rules = docs
                .Where(d => d.Enabled)
                .OrderByDescending(d => d.Priority)
                .ThenBy(d => d.RuleId, StringComparer.Ordinal)
                .Select(ToManageItem)
                .ToList()
        };
    }

    private async Task EnsureSeededAsync(string databaseName, CancellationToken ct)
    {
        await _seedGate.WaitAsync(ct);
        try
        {
            await SyncBuiltinSeedAsync(databaseName, ct);
        }
        finally
        {
            _seedGate.Release();
        }
    }

    /// <summary>
    /// Inserts missing builtins and, when <see cref="SecEventParseRuleCatalogSeed.SeedRevision"/>
    /// advances, refreshes builtin match/extract (preserves Enabled).
    /// </summary>
    private async Task SyncBuiltinSeedAsync(string databaseName, CancellationToken ct)
    {
        var meta = await _store.GetMetaAsync(databaseName, ct);
        var previousRevision = meta?.BuiltinSeedRevision ?? 0;
        var refreshAll = previousRevision < SecEventParseRuleCatalogSeed.SeedRevision;
        var inserted = 0;
        var refreshed = 0;
        var removed = 0;

        var seedDocs = SecEventParseRuleCatalogSeed.CreateSeedDocuments();
        var seedIds = new HashSet<string>(
            seedDocs.Select(d => d.RuleId),
            StringComparer.Ordinal);

        foreach (var seed in seedDocs)
        {
            var existing = await _store.GetByRuleIdAsync(databaseName, seed.RuleId, ct);
            if (existing is null)
            {
                await _store.UpsertAsync(databaseName, seed, ct);
                inserted++;
                continue;
            }

            if (!existing.Builtin || !refreshAll)
                continue;

            seed.Id = existing.Id;
            seed.Enabled = existing.Enabled;
            seed.CreatedAtUtc = existing.CreatedAtUtc;
            seed.Version = Math.Max(existing.Version + 1, SecEventParseRuleCatalogSeed.SeedRevision);
            seed.UpdatedAtUtc = DateTime.UtcNow;
            await _store.UpsertAsync(databaseName, seed, ct);
            refreshed++;
        }

        if (refreshAll)
        {
            foreach (var doc in await _store.ListAsync(databaseName, ct))
            {
                if (!doc.Builtin || seedIds.Contains(doc.RuleId))
                    continue;
                if (await _store.DeleteByRuleIdAsync(databaseName, doc.RuleId, ct))
                    removed++;
            }
        }

        if (refreshAll || inserted > 0 || refreshed > 0 || removed > 0)
            await RegisterSeedCustomFieldsAsync(databaseName, ct);

        var needMetaWrite = meta is null
                            || previousRevision < SecEventParseRuleCatalogSeed.SeedRevision
                            || inserted > 0
                            || refreshed > 0
                            || removed > 0;
        if (needMetaWrite)
        {
            meta ??= new SecEventParseCatalogMetaDocument
            {
                Version = SecEventParseRuleCatalogSeed.InitialVersion,
                PublishedUtc = DateTime.UtcNow
            };
            meta.BuiltinSeedRevision = SecEventParseRuleCatalogSeed.SeedRevision;
            await _store.SaveMetaAsync(databaseName, meta, ct);
        }

        if (inserted > 0 || refreshed > 0 || removed > 0)
        {
            var domain = databaseName.StartsWith("mng_", StringComparison.OrdinalIgnoreCase)
                ? databaseName["mng_".Length..]
                : databaseName;
            _cache.Invalidate(domain);
            _logger.LogInformation(
                "Sec-event parse builtins synced database={Database} revision={Revision} inserted={Inserted} refreshed={Refreshed} removed={Removed}",
                databaseName,
                SecEventParseRuleCatalogSeed.SeedRevision,
                inserted,
                refreshed,
                removed);
        }
    }

    private async Task RegisterSeedCustomFieldsAsync(string databaseName, CancellationToken ct)
    {
        foreach (var seed in SecEventParseRuleCatalogSeed.CreateSeedDocuments())
        {
            var dtoExtract = seed.Extract.Select(e => new SecEventParseRuleExtractStepDto
            {
                Type = e.Type,
                From = e.From,
                To = e.To,
                Value = e.Value,
                Pattern = e.Pattern,
                Groups = e.Groups
            }).ToList();
            await RegisterCustomTargetsAsync(databaseName, dtoExtract, ct);
        }
    }

    private static string Db(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            throw new ArgumentException("Domain is required.");
        return $"mng_{domain.Trim().ToLowerInvariant()}";
    }

    private static SecEventParseRuleDocument FromUpsert(
        SecEventParseRuleUpsertRequest request,
        string ruleId,
        bool builtin,
        int version,
        DateTime createdAt,
        string? existingMongoId)
    {
        return new SecEventParseRuleDocument
        {
            Id = string.IsNullOrWhiteSpace(existingMongoId)
                ? MongoDB.Bson.ObjectId.GenerateNewId().ToString()
                : existingMongoId,
            RuleId = ruleId,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Enabled = request.Enabled,
            Priority = request.Priority,
            Builtin = builtin,
            Version = version,
            Match = MapMatch(request.Match),
            Extract = (request.Extract ?? []).Select(MapExtract).ToList(),
            OnConflict = "first_wins",
            CreatedAtUtc = createdAt,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    private static SecEventParseRuleMatch MapMatch(SecEventParseRuleMatchDto dto) => new()
    {
        SourceProduct = dto.SourceProduct.Select(x => x.Trim().ToLowerInvariant()).Where(x => x.Length > 0).Distinct(StringComparer.Ordinal).ToList(),
        SourceType = NormalizeOptionalList(dto.SourceType),
        Channel = NormalizeOptionalList(dto.Channel, toLower: false),
        EventIds = dto.EventIds?.Distinct().OrderBy(x => x).ToList(),
        When = dto.When?.Select(w => new SecEventParseRuleWhen
        {
            Field = w.Field.Trim(),
            Op = w.Op.Trim().ToLowerInvariant(),
            Value = w.Value,
            Values = w.Values
        }).ToList(),
        MessagePatterns = dto.MessagePatterns?.Select(m => new SecEventParseRuleMessagePattern
        {
            Family = m.Family.Trim().ToLowerInvariant()
        }).ToList()
    };

    private static List<string>? NormalizeOptionalList(List<string>? values, bool toLower = true)
    {
        if (values is null || values.Count == 0)
            return null;
        var list = values
            .Select(x => toLower ? x.Trim().ToLowerInvariant() : x.Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return list.Count == 0 ? null : list;
    }

    private static SecEventParseRuleExtractStep MapExtract(SecEventParseRuleExtractStepDto dto) => new()
    {
        Type = dto.Type.Trim().ToLowerInvariant(),
        From = dto.From?.Trim(),
        To = dto.To?.Trim(),
        Value = dto.Value,
        Pattern = dto.Pattern,
        Groups = dto.Groups is null
            ? null
            : new Dictionary<string, string>(dto.Groups, StringComparer.Ordinal)
    };

    private static SecEventParseRuleManageItemDto ToManageItem(SecEventParseRuleDocument doc) => new()
    {
        Id = doc.Id,
        RuleId = doc.RuleId,
        Name = doc.Name,
        Description = doc.Description,
        Enabled = doc.Enabled,
        Priority = doc.Priority,
        Builtin = doc.Builtin,
        Version = doc.Version,
        Match = new SecEventParseRuleMatchDto
        {
            SourceProduct = doc.Match.SourceProduct.ToList(),
            SourceType = doc.Match.SourceType?.ToList(),
            Channel = doc.Match.Channel?.ToList(),
            EventIds = doc.Match.EventIds?.ToList(),
            When = doc.Match.When?.Select(w => new SecEventParseRuleWhenDto
            {
                Field = w.Field,
                Op = w.Op,
                Value = w.Value,
                Values = w.Values?.ToList()
            }).ToList(),
            MessagePatterns = doc.Match.MessagePatterns?.Select(m => new SecEventParseRuleMessagePatternDto
            {
                Family = m.Family
            }).ToList()
        },
        Extract = doc.Extract.Select(e => new SecEventParseRuleExtractStepDto
        {
            Type = e.Type,
            From = e.From,
            To = e.To,
            Value = e.Value,
            Pattern = e.Pattern,
            Groups = e.Groups is null ? null : new Dictionary<string, string>(e.Groups)
        }).ToList(),
        OnConflict = doc.OnConflict,
        UpdatedAtUtc = doc.UpdatedAtUtc
    };

    internal static bool Matches(SecEventParseRuleDocument rule, SecEventParseRulePreviewContext ctx)
    {
        var product = ctx.Source?.Product?.Trim().ToLowerInvariant() ?? string.Empty;
        var type = ctx.Source?.Type?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!SecEventParseFieldResolver.MatchesSourceProduct(rule.Match.SourceProduct, product, type))
            return false;

        // Preview: empty source type is lenient (wizard may omit it).
        if (rule.Match.SourceType is { Count: > 0 } && !string.IsNullOrEmpty(type)
            && !SecEventParseFieldResolver.MatchesSourceType(rule.Match.SourceType, type))
            return false;

        if (rule.Match.Channel is { Count: > 0 })
        {
            var channel = ctx.Channel?.Trim()
                          ?? (TryGetRoot(ctx.Raw, out var root)
                              ? SecEventParseFieldResolver.ReadChannel(root)
                              : null)
                          ?? string.Empty;
            if (!string.IsNullOrEmpty(channel)
                && !rule.Match.Channel.Any(c => string.Equals(c, channel, StringComparison.OrdinalIgnoreCase)))
                return false;
        }

        if (rule.Match.EventIds is { Count: > 0 })
        {
            int? eventId = ctx.EventId;
            if (!eventId.HasValue && TryGetRoot(ctx.Raw, out var root))
                eventId = SecEventParseFieldResolver.ReadEventId(root);
            if (!eventId.HasValue || !rule.Match.EventIds.Contains(eventId.Value))
                return false;
        }

        if (rule.Match.When is { Count: > 0 })
        {
            foreach (var when in rule.Match.When)
            {
                if (!EvaluateWhen(when, ctx))
                    return false;
            }
        }

        // messagePatterns.family is a soft tag for UI/seed; regex extract still does the work.
        return true;
    }

    private static bool EvaluateWhen(SecEventParseRuleWhen when, SecEventParseRulePreviewContext ctx)
    {
        var rawValue = ReadField(ctx, when.Field);
        var op = when.Op.Trim().ToLowerInvariant();
        return op switch
        {
            "exists" => !string.IsNullOrEmpty(rawValue),
            "eq" => string.Equals(rawValue, when.Value, StringComparison.OrdinalIgnoreCase),
            "neq" => !string.Equals(rawValue, when.Value, StringComparison.OrdinalIgnoreCase),
            "in" => when.Values?.Any(v => string.Equals(rawValue, v, StringComparison.OrdinalIgnoreCase)) == true,
            _ => false
        };
    }

    private static void ApplyExtract(
        SecEventParseRuleDocument rule,
        SecEventParseRulePreviewContext ctx,
        SecEventParseRulePreviewResponse response)
    {
        foreach (var step in rule.Extract)
        {
            try
            {
                switch (step.Type.ToLowerInvariant())
                {
                    case "constant":
                        if (!string.IsNullOrWhiteSpace(step.To))
                            response.Fields[step.To!] = Coerce(step.To!, step.Value);
                        break;
                    case "event_data":
                    case "json_path":
                    {
                        var from = step.From ?? string.Empty;
                        string? value = null;
                        if (TryGetRoot(ctx.Raw, out var root))
                        {
                            value = step.Type.Equals("event_data", StringComparison.OrdinalIgnoreCase)
                                ? SecEventParseFieldResolver.ReadEventData(root, from)
                                : SecEventParseFieldResolver.ReadPath(root, from)
                                  ?? SecEventParseFieldResolver.ReadPath(root, $"fields.{from}");
                        }

                        value ??= ReadField(ctx, from);
                        if (value is not null && !string.IsNullOrWhiteSpace(step.To))
                            response.Fields[step.To!] = Coerce(step.To!, value);
                        break;
                    }
                    case "regex":
                    {
                        string? input;
                        if (string.IsNullOrWhiteSpace(step.From) || step.From == "message")
                        {
                            input = ctx.Message;
                            if (string.IsNullOrEmpty(input) && TryGetRoot(ctx.Raw, out var msgRoot))
                                input = SecEventParseFieldResolver.ReadMessage(msgRoot);
                            input ??= ReadField(ctx, "message") ?? RawAsString(ctx.Raw);
                        }
                        else
                        {
                            input = ReadField(ctx, step.From!);
                        }

                        if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(step.Pattern) || step.Groups is null)
                            break;
                        var match = Regex.Match(
                            input,
                            step.Pattern,
                            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                            TimeSpan.FromMilliseconds(250));
                        if (!match.Success)
                        {
                            response.Notes.Add($"Regex did not match for rule step targeting {string.Join(',', step.Groups.Values)}.");
                            break;
                        }

                        foreach (var (groupKey, target) in step.Groups)
                        {
                            var g = int.TryParse(groupKey, out var idx)
                                ? (idx >= 0 && idx < match.Groups.Count ? match.Groups[idx].Value : null)
                                : match.Groups[groupKey].Success ? match.Groups[groupKey].Value : null;
                            if (g is not null)
                                response.Fields[target] = Coerce(target, g);
                        }

                        break;
                    }
                    case "kv":
                    {
                        var line = RawAsString(ctx.Raw) ?? ctx.Message ?? string.Empty;
                        if (step.Groups is { Count: > 0 })
                        {
                            foreach (var (key, target) in step.Groups)
                            {
                                var v = ReadKv(line, key);
                                if (v is not null)
                                    response.Fields[target] = Coerce(target, v);
                            }
                        }
                        else if (!string.IsNullOrWhiteSpace(step.From) && !string.IsNullOrWhiteSpace(step.To))
                        {
                            var v = ReadKv(line, step.From!);
                            if (v is not null)
                                response.Fields[step.To!] = Coerce(step.To!, v);
                        }

                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                response.Notes.Add($"Extract step '{step.Type}' failed: {ex.Message}");
            }
        }
    }

    private static object? Coerce(string target, string? value)
    {
        if (value is null)
            return null;
        if (target == "network.dstPort" && int.TryParse(value, out var port))
            return port;
        if (target == "event.severity" && int.TryParse(value, out var sev))
            return sev;
        if (target == "tags")
            return value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
        return value;
    }

    private static string? ReadField(SecEventParseRulePreviewContext ctx, string field)
    {
        if (string.Equals(field, "message", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(ctx.Message))
                return ctx.Message;
            if (TryGetRoot(ctx.Raw, out var msgRoot))
                return SecEventParseFieldResolver.ReadMessage(msgRoot);
            return RawAsString(ctx.Raw);
        }

        if (!TryGetRoot(ctx.Raw, out var root))
            return null;

        if (field.StartsWith("EventData.", StringComparison.OrdinalIgnoreCase)
            || field.StartsWith("eventData.", StringComparison.OrdinalIgnoreCase))
        {
            var key = field.Split('.', 2)[1];
            return SecEventParseFieldResolver.ReadEventData(root, key);
        }

        return SecEventParseFieldResolver.ReadPath(root, field)
               ?? SecEventParseFieldResolver.ReadPath(root, $"fields.{field}");
    }

    private static bool TryGetRoot(object? raw, out JsonElement root)
    {
        root = default;
        if (raw is null)
            return false;
        try
        {
            if (raw is JsonElement el)
            {
                root = el.Clone();
                return root.ValueKind == JsonValueKind.Object;
            }

            var text = RawAsString(raw);
            if (string.IsNullOrWhiteSpace(text))
                return false;
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return false;
            root = doc.RootElement.Clone();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? RawAsString(object? raw)
    {
        if (raw is null)
            return null;
        if (raw is string s)
            return s;
        if (raw is JsonElement el)
            return el.ValueKind == JsonValueKind.String ? el.GetString() : el.GetRawText();
        return JsonSerializer.Serialize(raw);
    }

    private static string? ReadKv(string line, string key)
    {
        if (string.IsNullOrEmpty(line) || string.IsNullOrEmpty(key))
            return null;
        var token = key + "=";
        var idx = line.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return null;
        var start = idx + token.Length;
        var end = start;
        while (end < line.Length && !char.IsWhiteSpace(line[end]))
            end++;
        return line[start..end];
    }

    public async Task<SecEventTargetFieldCatalogResponse> GetTargetFieldsAsync(
        string domain,
        CancellationToken ct = default)
    {
        var db = Db(domain);
        await EnsureSeededAsync(db, ct);
        var customDocs = await _store.ListCustomFieldsAsync(db, ct);
        var custom = customDocs.Select(d => SecEventTargetFieldCatalog.ToCustomDefinition(
            d.Name, d.Label, d.ValueType, d.Description));
        return SecEventTargetFieldCatalog.ToResponse(custom);
    }

    public async Task<SecEventTargetFieldDefinition> UpsertCustomFieldAsync(
        string domain,
        SecEventCustomFieldUpsertRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var db = Db(domain);
        await EnsureSeededAsync(db, ct);

        var name = SecEventTargetFieldCatalog.NormalizeCustomFieldName(request.Name);
        var existing = (await _store.ListCustomFieldsAsync(db, ct))
            .FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.Ordinal));
        var now = DateTime.UtcNow;
        var doc = new SecEventCustomFieldDocument
        {
            Name = name,
            Label = string.IsNullOrWhiteSpace(request.Label)
                ? (existing?.Label ?? SecEventTargetFieldCatalog.LabelFromName(name))
                : request.Label.Trim(),
            ValueType = string.IsNullOrWhiteSpace(request.ValueType)
                ? (existing?.ValueType ?? "keyword")
                : request.ValueType.Trim().ToLowerInvariant(),
            Description = request.Description ?? existing?.Description,
            CreatedAtUtc = existing?.CreatedAtUtc ?? now,
            UpdatedAtUtc = now
        };
        await _store.UpsertCustomFieldAsync(db, doc, ct);
        return SecEventTargetFieldCatalog.ToCustomDefinition(
            doc.Name, doc.Label, doc.ValueType, doc.Description);
    }

    public async Task DeleteCustomFieldAsync(string domain, string name, CancellationToken ct = default)
    {
        var db = Db(domain);
        await EnsureSeededAsync(db, ct);
        var key = SecEventTargetFieldCatalog.NormalizeCustomFieldName(name);
        if (!await _store.DeleteCustomFieldAsync(db, key, ct))
            throw new InvalidOperationException($"Custom field '{key}' not found.");
    }

    private async Task RegisterCustomTargetsAsync(
        string databaseName,
        List<SecEventParseRuleExtractStepDto>? extract,
        CancellationToken ct)
    {
        foreach (var name in SecEventParseRuleValidator.CollectCustomTargets(extract))
        {
            var existing = (await _store.ListCustomFieldsAsync(databaseName, ct))
                .FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.Ordinal));
            if (existing is not null)
                continue;
            var now = DateTime.UtcNow;
            await _store.UpsertCustomFieldAsync(
                databaseName,
                new SecEventCustomFieldDocument
                {
                    Name = name,
                    Label = SecEventTargetFieldCatalog.LabelFromName(name),
                    ValueType = "keyword",
                    Description = "Auto-registered from parse rule extract.",
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                },
                ct);
        }
    }
}
