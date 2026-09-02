using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MngAlarm.Application.Contracts;
using MngAlarm.Application.Observations;
using MngAlarm.Application.Services;
using MngAlarm.Domain.Entities;
using MngAlarm.Infrastructure.Evaluation;
using System.Text.Json;

namespace MngAlarm.Infrastructure.Services;

public sealed class ScenarioService(
    IAlarmDomainAccessor domain,
    IScenarioRepository scenarios,
    IAlarmRuleRepository rules,
    IScenarioExecutionRepository? executions = null,
    IScenarioRuntimeCapabilities? capabilities = null) : IScenarioService
{
    public async Task<IReadOnlyList<ScenarioCatalogItem>> ListAsync(
        bool includeDrafts,
        CancellationToken cancellationToken = default)
    {
        var ctx = domain.GetRequiredDomain();
        var versions = await scenarios.ListAsync(ctx.DomainName, cancellationToken);
        var ruleHealth = (await rules.ListAllAsync(ctx.DomainName, cancellationToken))
            .Where(x => !string.IsNullOrWhiteSpace(x.ScenarioId))
            .GroupBy(x => x.ScenarioId!, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.UpdatedAt).First().RuntimeHealth,
                StringComparer.Ordinal);

        return versions
            .GroupBy(x => x.ScenarioId, StringComparer.Ordinal)
            .Select(group =>
            {
                var ordered = group.OrderByDescending(x => x.Version).ToList();
                var latest = includeDrafts
                    ? ordered[0]
                    : ordered.FirstOrDefault(x => x.Status == ScenarioLifecycleStatuses.Published);
                if (latest == null) return null;
                var published = ordered.FirstOrDefault(x => x.Status == ScenarioLifecycleStatuses.Published);
                var health = published != null && ruleHealth.TryGetValue(group.Key, out var rh) ? rh : null;
                return new ScenarioCatalogItem
                {
                    ScenarioId = group.Key,
                    Name = latest.Name,
                    LatestVersion = latest.Version,
                    LatestStatus = latest.Status,
                    PublishedVersion = published?.Version,
                    DraftVersion = ordered.FirstOrDefault(x => x.Status is ScenarioLifecycleStatuses.Draft or ScenarioLifecycleStatuses.Validated)?.Version,
                    Enabled = published?.Enabled ?? false,
                    OperationalStatus = ScenarioHealthTracker.ResolveOperationalStatus(published, latest),
                    Health = health?.Level ?? ScenarioHealthLevels.Unknown,
                    LastErrorMessage = health?.LastErrorMessage,
                    LastErrorAt = health?.LastErrorAt,
                    LastSuccessAt = health?.LastSuccessAt,
                    Severity = latest.Severity,
                    Origin = latest.Origin,
                    IsReadOnly = latest.IsReadOnly,
                    TemplateId = latest.TemplateId,
                    PackageId = latest.PackageId,
                    PackageVersion = latest.PackageVersion,
                    UpdatedAt = latest.UpdatedAt
                };
            })
            .Where(x => x != null)
            .Cast<ScenarioCatalogItem>()
            .OrderByDescending(x => x.UpdatedAt)
            .ToList();
    }

    public async Task<ScenarioVersionDocument> CreateDraftAsync(
        CreateScenarioDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        var ctx = domain.GetRequiredDomain();
        var draft = new ScenarioVersionDocument
        {
            DomainId = ctx.DomainId,
            DomainName = ctx.DomainName,
            Name = request.Name.Trim(),
            Severity = request.Severity,
            Enabled = request.Enabled,
            Definition = NormalizeDefinition(request.Definition),
            Status = ScenarioLifecycleStatuses.Draft
        };
        AlignAlarmSeverity(draft, request.Severity);
        await scenarios.InsertVersionAsync(draft, cancellationToken);
        await AuditAsync(draft, "draft.created", cancellationToken);
        return draft;
    }

    public async Task<ScenarioVersionDocument?> CreateNextDraftAsync(
        string scenarioId,
        CreateScenarioDraftRequest? request,
        CancellationToken cancellationToken = default)
    {
        var ctx = domain.GetRequiredDomain();
        var latest = await scenarios.GetLatestAsync(ctx.DomainName, scenarioId, cancellationToken);
        if (latest == null || latest.IsReadOnly || latest.Origin == ScenarioOrigins.Product)
            return null;

        var draft = new ScenarioVersionDocument
        {
            ScenarioId = scenarioId,
            DomainId = latest.DomainId,
            DomainName = latest.DomainName,
            Version = latest.Version + 1,
            Status = ScenarioLifecycleStatuses.Draft,
            Name = request == null || string.IsNullOrWhiteSpace(request.Name) ? latest.Name : request.Name.Trim(),
            Severity = request?.Severity ?? latest.Severity,
            Enabled = request?.Enabled ?? latest.Enabled,
            Definition = request == null ? Clone(latest.Definition) : NormalizeDefinition(request.Definition)
        };
        AlignAlarmSeverity(draft, request?.Severity);
        await scenarios.InsertVersionAsync(draft, cancellationToken);
        await AuditAsync(draft, "draft.created", cancellationToken);
        return draft;
    }

    public async Task<ScenarioVersionDocument?> CloneTemplateAsync(
        string scenarioId,
        int version,
        CancellationToken cancellationToken = default)
    {
        var ctx = domain.GetRequiredDomain();
        var template = await scenarios.GetVersionAsync(ctx.DomainName, scenarioId, version, cancellationToken);
        if (template == null || template.Origin != ScenarioOrigins.Product || !template.IsReadOnly)
            return null;

        var draft = new ScenarioVersionDocument
        {
            DomainId = ctx.DomainId,
            DomainName = ctx.DomainName,
            Name = template.Name,
            Severity = template.Severity,
            Enabled = false,
            Origin = ScenarioOrigins.User,
            IsReadOnly = false,
            TemplateId = template.TemplateId,
            PackageId = template.PackageId,
            PackageVersion = template.PackageVersion,
            Definition = Clone(template.Definition)
        };
        await scenarios.InsertVersionAsync(draft, cancellationToken);
        await AuditAsync(draft, $"template.cloned.{template.ScenarioId}.{version}", cancellationToken);
        return draft;
    }

    public async Task<ScenarioPackageImportResult> ImportProductPackageAsync(
        ImportScenarioPackageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.PackageId)
            || string.IsNullOrWhiteSpace(request.PackageVersion)
            || request.Templates.Count == 0)
            throw new ArgumentException("Package id, version and templates are required.");

        var ctx = domain.GetRequiredDomain();
        var existing = (await scenarios.ListAsync(ctx.DomainName, cancellationToken)).ToList();
        var created = new List<string>();
        var skipped = 0;
        foreach (var template in request.Templates)
        {
            if (string.IsNullOrWhiteSpace(template.TemplateId))
                throw new ArgumentException("Every product template requires templateId.");
            if (existing.Any(x => x.Origin == ScenarioOrigins.Product
                && x.PackageId == request.PackageId
                && x.PackageVersion == request.PackageVersion
                && x.TemplateId == template.TemplateId))
            {
                skipped++;
                continue;
            }

            var normalizedDefinition = NormalizeDefinition(template.Definition);
            var validation = ScenarioCompiler.Validate(normalizedDefinition, false);
            if (!validation.IsValid)
                throw new ArgumentException($"Template '{template.TemplateId}' is invalid: {validation.Diagnostics[0].Code}");

            var priorTemplate = existing
                .Where(x => x.Origin == ScenarioOrigins.Product
                    && x.PackageId == request.PackageId
                    && x.TemplateId == template.TemplateId)
                .OrderByDescending(x => x.Version)
                .FirstOrDefault();
            var item = new ScenarioVersionDocument
            {
                ScenarioId = priorTemplate?.ScenarioId ?? $"product:{request.PackageId}:{template.TemplateId}",
                DomainId = ctx.DomainId,
                DomainName = ctx.DomainName,
                Version = (priorTemplate?.Version ?? 0) + 1,
                Status = ScenarioLifecycleStatuses.Published,
                Name = template.Name.Trim(),
                Severity = template.Severity,
                Enabled = false,
                Origin = ScenarioOrigins.Product,
                IsReadOnly = true,
                TemplateId = template.TemplateId.Trim(),
                PackageId = request.PackageId.Trim(),
                PackageVersion = request.PackageVersion.Trim(),
                Definition = normalizedDefinition,
                Validation = validation,
                PublishedAt = DateTime.UtcNow
            };
            await scenarios.ArchivePublishedExceptAsync(
                item.DomainName,
                item.ScenarioId,
                item.Version,
                item.PublishedAt.Value,
                cancellationToken);
            await scenarios.InsertVersionAsync(item, cancellationToken);
            await AuditAsync(item, "template.imported", cancellationToken);
            existing.Add(item);
            created.Add(item.ScenarioId);
        }

        return new ScenarioPackageImportResult
        {
            Created = created.Count,
            Skipped = skipped,
            ScenarioIds = created
        };
    }

    public async Task<ScenarioVersionDocument?> UpdateDraftAsync(
        string scenarioId,
        int version,
        UpdateScenarioDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        var ctx = domain.GetRequiredDomain();
        var draft = await scenarios.GetVersionAsync(ctx.DomainName, scenarioId, version, cancellationToken);
        if (draft == null
            || draft.Status != ScenarioLifecycleStatuses.Draft
            || draft.IsReadOnly
            || draft.Origin == ScenarioOrigins.Product)
            return null;

        if (!string.IsNullOrWhiteSpace(request.Name)) draft.Name = request.Name.Trim();
        if (request.Severity.HasValue) draft.Severity = request.Severity.Value;
        if (request.Enabled.HasValue) draft.Enabled = request.Enabled.Value;
        if (request.Definition != null) draft.Definition = NormalizeDefinition(request.Definition);
        AlignAlarmSeverity(draft, request.Severity);
        draft.Validation = null;
        draft.UpdatedAt = DateTime.UtcNow;
        await scenarios.UpdateVersionAsync(draft, cancellationToken);
        await AuditAsync(draft, "draft.updated", cancellationToken);
        return draft;
    }

    public async Task<ScenarioVersionDocument?> GetAsync(
        string scenarioId,
        int? version,
        CancellationToken cancellationToken = default)
    {
        var ctx = domain.GetRequiredDomain();
        return version.HasValue
            ? await scenarios.GetVersionAsync(ctx.DomainName, scenarioId, version.Value, cancellationToken)
            : await scenarios.GetLatestAsync(ctx.DomainName, scenarioId, cancellationToken);
    }

    public async Task<ScenarioValidationSnapshot?> ValidateAsync(
        string scenarioId,
        int version,
        CancellationToken cancellationToken = default)
    {
        var item = await GetAsync(scenarioId, version, cancellationToken);
        if (item == null || item.Status == ScenarioLifecycleStatuses.Published || item.Status == ScenarioLifecycleStatuses.Archived)
            return null;

        if (item.IsReadOnly || item.Origin == ScenarioOrigins.Product)
            return null;

        item.Validation = await ValidateForRuntimeAsync(item, cancellationToken);
        item.Status = item.Validation.IsValid ? ScenarioLifecycleStatuses.Validated : ScenarioLifecycleStatuses.Draft;
        item.UpdatedAt = DateTime.UtcNow;
        await scenarios.UpdateVersionAsync(item, cancellationToken);
        await AuditAsync(item, item.Validation.IsValid ? "validation.succeeded" : "validation.failed", cancellationToken);
        return item.Validation;
    }

    public async Task<ScenarioVersionDocument?> PublishAsync(
        string scenarioId,
        int version,
        CancellationToken cancellationToken = default)
    {
        var item = await GetAsync(scenarioId, version, cancellationToken);
        if (item == null || item.Status != ScenarioLifecycleStatuses.Validated)
            return null;

        if (item.IsReadOnly || item.Origin == ScenarioOrigins.Product)
            return null;

        var validation = await ValidateForRuntimeAsync(item, cancellationToken);
        if (!validation.IsValid)
        {
            item.Validation = validation;
            item.Status = ScenarioLifecycleStatuses.Draft;
            await scenarios.UpdateVersionAsync(item, cancellationToken);
            return item;
        }

        var now = DateTime.UtcNow;
        await scenarios.ArchivePublishedExceptAsync(item.DomainName, item.ScenarioId, item.Version, now, cancellationToken);
        item.Enabled = false;
        var projections = (await rules.ListAllAsync(item.DomainName, cancellationToken))
            .Where(x => x.ScenarioId == item.ScenarioId)
            .ToList();
        var projection = projections.FirstOrDefault();
        foreach (var duplicate in projections.Skip(1))
        {
            duplicate.Enabled = false;
            duplicate.UpdatedAt = now;
            await rules.UpdateAsync(duplicate, cancellationToken);
        }
        if (projection == null)
        {
            projection = new AlarmRuleDocument
            {
                DomainId = item.DomainId,
                DomainName = item.DomainName,
                ScenarioId = item.ScenarioId,
                CreatedAt = DateTime.UtcNow
            };
            ApplyProjection(projection, item);
            await rules.InsertAsync(projection, cancellationToken);
        }
        else
        {
            ApplyProjection(projection, item);
            await rules.UpdateAsync(projection, cancellationToken);
        }

        item.Status = ScenarioLifecycleStatuses.Published;
        item.PublishedAt = now;
        item.UpdatedAt = item.PublishedAt.Value;
        item.Validation = validation;
        await scenarios.UpdateVersionAsync(item, cancellationToken);
        await AuditAsync(item, "version.published", cancellationToken);
        return item;
    }

    public async Task<ScenarioVersionDocument?> SetEnabledAsync(
        string scenarioId,
        int version,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        var item = await GetAsync(scenarioId, version, cancellationToken);
        if (item == null
            || item.Status != ScenarioLifecycleStatuses.Published
            || item.IsReadOnly
            || item.Origin == ScenarioOrigins.Product)
            return null;

        var now = DateTime.UtcNow;
        item.Enabled = enabled;
        item.UpdatedAt = now;
        await scenarios.UpdatePublishedEnabledAsync(item.DomainName, item.Id, enabled, now, cancellationToken);

        var projections = (await rules.ListAllAsync(item.DomainName, cancellationToken))
            .Where(x => x.ScenarioId == item.ScenarioId)
            .ToList();
        foreach (var projection in projections)
        {
            projection.Enabled = enabled && projection.ScenarioVersion == item.Version;
            projection.UpdatedAt = now;
            await rules.UpdateAsync(projection, cancellationToken);
        }

        await AuditAsync(item, enabled ? "version.enabled" : "version.disabled", cancellationToken);
        return item;
    }

    public async Task<ScenarioVersionDocument?> RollbackAsync(
        string scenarioId,
        int version,
        CancellationToken cancellationToken = default)
    {
        var ctx = domain.GetRequiredDomain();
        var source = await scenarios.GetVersionAsync(ctx.DomainName, scenarioId, version, cancellationToken);
        if (source == null
            || source.Status is not (ScenarioLifecycleStatuses.Published or ScenarioLifecycleStatuses.Archived)
            || source.IsReadOnly)
            return null;

        var latest = await scenarios.GetLatestAsync(ctx.DomainName, scenarioId, cancellationToken);
        var rollback = new ScenarioVersionDocument
        {
            ScenarioId = source.ScenarioId,
            DomainId = source.DomainId,
            DomainName = source.DomainName,
            Version = (latest?.Version ?? 0) + 1,
            Status = ScenarioLifecycleStatuses.Draft,
            Name = source.Name,
            Severity = source.Severity,
            Enabled = source.Enabled,
            Definition = Clone(source.Definition)
        };
        await scenarios.InsertVersionAsync(rollback, cancellationToken);
        await AuditAsync(rollback, $"rollback.from.{version}", cancellationToken);
        return rollback;
    }

    public async Task<ScenarioVersionDocument?> ArchiveAsync(
        string scenarioId,
        int version,
        CancellationToken cancellationToken = default)
    {
        var item = await GetAsync(scenarioId, version, cancellationToken);
        if (item == null
            || item.IsReadOnly
            || item.Origin == ScenarioOrigins.Product
            || item.Status == ScenarioLifecycleStatuses.Archived)
            return null;

        var archivable = item.Status is ScenarioLifecycleStatuses.Draft
            or ScenarioLifecycleStatuses.Validated
            or ScenarioLifecycleStatuses.Published;
        if (!archivable)
            return null;

        if (item.Status == ScenarioLifecycleStatuses.Published && item.Enabled)
            return null;

        item.Status = ScenarioLifecycleStatuses.Archived;
        item.UpdatedAt = DateTime.UtcNow;
        await scenarios.ArchiveVersionAsync(item.DomainName, scenarioId, version, item.UpdatedAt, cancellationToken);

        var projection = (await rules.ListAllAsync(item.DomainName, cancellationToken))
            .FirstOrDefault(x => x.ScenarioId == scenarioId && x.ScenarioVersion == version);
        if (projection != null)
        {
            projection.Enabled = false;
            projection.UpdatedAt = item.UpdatedAt;
            await rules.UpdateAsync(projection, cancellationToken);
        }

        await AuditAsync(item, "version.archived", cancellationToken);
        return item;
    }

    public async Task<IReadOnlyList<ScenarioAuditDocument>> AuditAsync(
        string scenarioId,
        CancellationToken cancellationToken = default)
    {
        var ctx = domain.GetRequiredDomain();
        return await scenarios.ListAuditAsync(ctx.DomainName, scenarioId, cancellationToken);
    }

    public async Task<IReadOnlyList<ScenarioExecutionDto>> ListExecutionsAsync(
        string scenarioId,
        int limit = ScenarioExecutionDocument.DefaultRetainCount,
        CancellationToken cancellationToken = default)
    {
        if (executions == null) return [];
        var ctx = domain.GetRequiredDomain();
        var items = await executions.ListRecentAsync(ctx.DomainName, scenarioId, limit, cancellationToken);
        return items.Select(x => new ScenarioExecutionDto
        {
            Id = x.Id,
            ScenarioId = x.ScenarioId,
            ScenarioVersion = x.ScenarioVersion,
            RuleId = x.RuleId,
            Trigger = x.Trigger,
            Outcome = x.Outcome,
            StartedAt = x.StartedAt,
            FinishedAt = x.FinishedAt,
            DurationMs = x.DurationMs,
            ObservationKind = x.ObservationKind,
            ObservationKey = x.ObservationKey,
            ObservationValue = x.ObservationValue,
            AlarmsRaised = x.AlarmsRaised,
            AlarmsUpdated = x.AlarmsUpdated,
            OutputNodeIds = x.OutputNodeIds,
            ErrorCode = x.ErrorCode,
            ErrorMessage = x.ErrorMessage,
            NodeTrace = x.NodeTrace.Select(t => new ScenarioExecutionTraceDto
            {
                NodeId = t.NodeId,
                NodeType = t.NodeType,
                Status = t.Status,
                Outcome = t.Outcome
            }).ToList()
        }).ToList();
    }

    public async Task<ScenarioPreviewResponse> PreviewAsync(
        string? scenarioId,
        int? version,
        ScenarioPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var definition = request.Definition;
        if (definition == null && !string.IsNullOrWhiteSpace(scenarioId))
            definition = (await GetAsync(scenarioId, version, cancellationToken))?.Definition;

        var validation = ScenarioCompiler.Validate(definition, false);
        if (definition == null || !validation.IsValid)
            return new ScenarioPreviewResponse { Supported = false, Diagnostics = validation.Diagnostics };

        if (request.Samples == null || request.Samples.Count == 0)
        {
            var diagnostics = validation.Diagnostics.ToList();
            diagnostics.Add(new ScenarioDiagnostic
            {
                Code = request.From.HasValue || request.To.HasValue ? "historical.unsupported" : "samples.required",
                Message = request.From.HasValue || request.To.HasValue
                    ? "Historical sec_events access is not available; provide request samples."
                    : "At least one sample observation is required.",
                Path = "samples"
            });
            return new ScenarioPreviewResponse { Supported = false, Diagnostics = diagnostics };
        }

        if (definition.SchemaVersion == 3)
            return PreviewGraph(definition, scenarioId, request);

        if (definition.Source.Kind == ScenarioSourceKinds.ScheduledStaleness)
            return PreviewStaleness(definition, scenarioId, request);

        var matches = new List<ScenarioPreviewMatch>();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var dedupKeys = new HashSet<string>(StringComparer.Ordinal);
        var sequenceStates = new Dictionary<string, (int Step, int Count, DateTime? Last)>(StringComparer.Ordinal);
        for (var i = 0; i < request.Samples.Count; i++)
        {
            var sample = request.Samples[i];
            var observation = new ObservationEnvelope
            {
                DomainId = "preview",
                DomainName = "preview",
                Kind = sample.Kind,
                Key = sample.Key,
                Value = sample.Value,
                Dimensions = ObservationValueNormalizer.NormalizeDimensions(sample.Dimensions),
                Timestamp = sample.Timestamp
            };
            var explanation = string.Empty;
            var groupKey = BuildGroupKey(definition.GroupBy, observation.Dimensions);
            var conditionMatched = false;

            if (definition.Sequence?.Steps.Count > 0)
            {
                var state = sequenceStates.GetValueOrDefault(groupKey);
                var step = definition.Sequence.Steps[Math.Clamp(state.Step, 0, definition.Sequence.Steps.Count - 1)];
                if (state.Last.HasValue && sample.Timestamp > state.Last.Value.AddSeconds(step.WithinSeconds))
                {
                    state = default;
                    step = definition.Sequence.Steps[0];
                }

                if (string.Equals(step.MatchKey, sample.Key, StringComparison.Ordinal)
                    && ScenarioCompiler.Matches(step.Condition, observation, out _))
                {
                    state.Count++;
                    state.Last = sample.Timestamp;
                    if (state.Count >= step.MinCount)
                    {
                        state.Step++;
                        state.Count = 0;
                    }

                    conditionMatched = state.Step == definition.Sequence.Steps.Count;
                    explanation = conditionMatched
                        ? $"Sequence completed at step {definition.Sequence.Steps.Count}."
                        : $"Sequence advanced; next step is {state.Step + 1}.";
                    if (conditionMatched) state = default;
                    sequenceStates[groupKey] = state;
                }
                else
                {
                    explanation = $"Expected sequence step {state.Step + 1} matchKey '{step.MatchKey}'.";
                }
            }
            else
            {
                var sourceMatched = ScenarioCompiler.SourceMatches(definition.Source, observation);
                conditionMatched = sourceMatched && ScenarioCompiler.Matches(definition.Condition, observation, out explanation);
                if (!sourceMatched)
                    explanation = $"Source matchKey '{definition.Source.MatchKey}' did not match '{sample.Key}'.";
            }

            if (conditionMatched)
            {
                counts[groupKey] = counts.GetValueOrDefault(groupKey) + 1;
                if (definition.Aggregation != null)
                {
                    var aggregateMatched = CompareAggregate(
                        counts[groupKey],
                        definition.Aggregation.Operator,
                        definition.Aggregation.Threshold);
                    explanation += $" Group count is {counts[groupKey]}; threshold matched={aggregateMatched}.";
                    conditionMatched = aggregateMatched;
                }
            }
            var dedupKey = BuildDedupKey(definition.Dedup.KeyTemplate, scenarioId ?? "preview", sample.Key, groupKey);
            if (conditionMatched) dedupKeys.Add(dedupKey);
            matches.Add(new ScenarioPreviewMatch
            {
                SampleIndex = i,
                Matched = conditionMatched,
                Explanation = explanation,
                GroupKey = groupKey,
                DedupKey = dedupKey
            });
        }

        return new ScenarioPreviewResponse
        {
            Diagnostics = validation.Diagnostics,
            Matches = matches,
            GroupCounts = counts,
            DedupKeys = dedupKeys.ToList()
        };
    }

    public async Task<ScenarioPreviewResponse> CompileAsync(
        string? scenarioId,
        int? version,
        ScenarioPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var definition = request.Definition;
        if (definition == null && !string.IsNullOrWhiteSpace(scenarioId))
            definition = (await GetAsync(scenarioId, version, cancellationToken))?.Definition;
        var validation = ScenarioCompiler.Validate(definition, false);
        IReadOnlyList<string> order = [];
        if (validation.IsValid && definition?.SchemaVersion == 3)
            order = ScenarioCompiler.CompileGraph(definition).TopologicalOrder;
        return new ScenarioPreviewResponse
        {
            Supported = validation.IsValid,
            Diagnostics = validation.Diagnostics,
            ExecutionOrder = order
        };
    }

    private async Task AuditAsync(ScenarioVersionDocument item, string action, CancellationToken cancellationToken) =>
        await scenarios.InsertAuditAsync(new ScenarioAuditDocument
        {
            ScenarioId = item.ScenarioId,
            DomainName = item.DomainName,
            Version = item.Version,
            Action = action
        }, cancellationToken);

    private async Task<ScenarioValidationSnapshot> ValidateForRuntimeAsync(
        ScenarioVersionDocument item,
        CancellationToken cancellationToken)
    {
        var validation = ScenarioCompiler.Validate(item.Definition, item.Enabled);
        if (!item.Enabled)
            return validation;

        if (item.Definition.Source.Kind == ScenarioSourceKinds.ScheduledQuery
            && capabilities?.ScheduledQueryAvailable != true)
            validation.Diagnostics.Add(new ScenarioDiagnostic
            {
                Code = "scheduled.provider.unavailable",
                Message = "Enabled publish requires a registered scheduled-query provider.",
                Path = "source.kind"
            });
        if (item.Definition.Source.Kind == ScenarioSourceKinds.MetaCorrelation
            && capabilities?.MetaCorrelationAvailable != true)
            validation.Diagnostics.Add(new ScenarioDiagnostic
            {
                Code = "meta.runtime.unavailable",
                Message = "Enabled publish requires meta-correlation runtime capability.",
                Path = "source.kind"
            });

        if (item.Definition.Source.Kind == ScenarioSourceKinds.MetaCorrelation)
            await ValidateMetaGraphAsync(item, validation.Diagnostics, cancellationToken);

        validation.IsValid = validation.Diagnostics.All(x => x.Severity != "error");
        return validation;
    }

    private async Task ValidateMetaGraphAsync(
        ScenarioVersionDocument item,
        List<ScenarioDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var all = await scenarios.ListAsync(item.DomainName, cancellationToken);
        var graph = all
            .GroupBy(x => x.ScenarioId)
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(v => v.Version).First().Definition.Source.DependsOnScenarioIds,
                StringComparer.Ordinal);
        graph[item.ScenarioId] = item.Definition.Source.DependsOnScenarioIds;
        var maxDepth = item.Definition.Source.MaxChainDepth;

        bool Visit(string current, HashSet<string> path, int depth)
        {
            if (depth > maxDepth) return true;
            if (!path.Add(current)) return true;
            if (graph.TryGetValue(current, out var dependencies))
            {
                foreach (var dependency in dependencies)
                    if (Visit(dependency, new HashSet<string>(path, StringComparer.Ordinal), depth + 1))
                        return true;
            }
            return false;
        }

        if (Visit(item.ScenarioId, new HashSet<string>(StringComparer.Ordinal), 0))
            diagnostics.Add(new ScenarioDiagnostic
            {
                Code = "meta.graph.cycle_or_depth",
                Message = "Meta-correlation graph contains a cycle or exceeds maxChainDepth.",
                Path = "source.dependsOnScenarioIds"
            });
    }

    private static void ApplyProjection(AlarmRuleDocument rule, ScenarioVersionDocument version)
    {
        AlignAlarmSeverity(version, preferredSeverity: null);
        rule.Name = version.Name;
        rule.Enabled = version.Enabled;
        rule.Severity = version.Severity;
        rule.ScenarioId = version.ScenarioId;
        rule.ScenarioVersion = version.Version;
        rule.UpdatedAt = DateTime.UtcNow;
        ScenarioCompiler.ApplyToLegacyFields(rule, NormalizeDefinition(version.Definition));
    }

    /// <summary>
    /// Keeps version.Severity and alarm-output node config.severity in sync so runtime
    /// (node.Config.Severity ?? rule.Severity) always reflects the intended value.
    /// Single-output graphs take preferredSeverity (or version) onto the node.
    /// Multi-output graphs only fill missing node severities so distinct values are kept.
    /// </summary>
    private static void AlignAlarmSeverity(ScenarioVersionDocument version, int? preferredSeverity)
    {
        var outputs = version.Definition?.Graph?.Nodes
            .Where(node => node.Type == ScenarioNodeTypes.AlarmOutput)
            .ToList() ?? [];

        if (preferredSeverity.HasValue)
            version.Severity = ClampSeverity(preferredSeverity.Value);

        if (outputs.Count == 0)
            return;

        if (outputs.Count == 1)
        {
            var only = outputs[0];
            if (preferredSeverity.HasValue || !only.Config.Severity.HasValue)
                only.Config.Severity = ClampSeverity(version.Severity);
            else
                version.Severity = ClampSeverity(only.Config.Severity.Value);
            return;
        }

        foreach (var output in outputs)
            output.Config.Severity ??= ClampSeverity(version.Severity);
    }

    private static int ClampSeverity(int severity) => Math.Clamp(severity, 1, 10);

    private static ScenarioDefinition NormalizeDefinition(ScenarioDefinition definition)
    {
        var normalized = JsonSerializer.Deserialize<ScenarioDefinition>(JsonSerializer.Serialize(definition))!;
        NormalizeValuesInPlace(normalized);
        return normalized;
    }

    private static void NormalizeValuesInPlace(ScenarioDefinition definition)
    {
        NormalizeCondition(definition.Condition);
        NormalizeSequence(definition.Sequence);
        if (definition.Graph != null)
        {
            foreach (var node in definition.Graph.Nodes)
            {
                NormalizeCondition(node.Config.Condition);
                NormalizeSequence(node.Config.Sequence);
            }
        }
    }

    private static void NormalizeCondition(ScenarioCondition? condition)
    {
        if (condition == null) return;
        condition.Value = ObservationValueNormalizer.Normalize(condition.Value);
        foreach (var child in condition.Children)
            NormalizeCondition(child);
    }

    private static void NormalizeSequence(ScenarioSequence? sequence)
    {
        if (sequence == null) return;
        foreach (var step in sequence.Steps)
            NormalizeCondition(step.Condition);
    }

    private static ScenarioDefinition Clone(ScenarioDefinition definition) =>
        NormalizeDefinition(definition);

    private static string BuildGroupKey(IEnumerable<string> fields, IReadOnlyDictionary<string, object?> dimensions)
    {
        var values = fields.Select(field => dimensions.TryGetValue(field, out var value) ? value?.ToString() ?? "_null" : "_missing").ToList();
        return values.Count == 0 ? "_all" : string.Join("|", values);
    }

    private static string BuildDedupKey(string template, string ruleId, string key, string groupKey) =>
        template.Replace("{ruleId}", ruleId, StringComparison.Ordinal)
            .Replace("{key}", key, StringComparison.Ordinal)
            .Replace("{groupKey}", groupKey, StringComparison.Ordinal);

    private static ScenarioPreviewResponse PreviewStaleness(
        ScenarioDefinition definition,
        string? scenarioId,
        ScenarioPreviewRequest request)
    {
        var matching = request.Samples!
            .Select((sample, index) => (Sample: sample, Index: index))
            .Where(x => string.Equals(x.Sample.Key, definition.Source.MatchKey, StringComparison.Ordinal)
                && (string.IsNullOrWhiteSpace(definition.Source.ObservationKind)
                    || string.Equals(x.Sample.Kind, definition.Source.ObservationKind, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        var evaluationTime = request.To
            ?? request.Samples!.Max(x => x.Timestamp);
        var staleness = TimeSpan.FromSeconds(definition.Window?.StalenessSeconds ?? 0);
        var matches = new List<ScenarioPreviewMatch>();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var dedup = new HashSet<string>(StringComparer.Ordinal);

        foreach (var group in matching.GroupBy(x => BuildGroupKey(definition.GroupBy, x.Sample.Dimensions)))
        {
            var latest = group.OrderByDescending(x => x.Sample.Timestamp).First();
            counts[group.Key] = group.Count();
            var stale = evaluationTime - latest.Sample.Timestamp > staleness;
            var dedupKey = BuildDedupKey(
                definition.Dedup.KeyTemplate,
                scenarioId ?? "preview",
                definition.Source.MatchKey,
                group.Key);
            if (stale) dedup.Add(dedupKey);
            matches.Add(new ScenarioPreviewMatch
            {
                SampleIndex = latest.Index,
                Matched = stale,
                GroupKey = group.Key,
                DedupKey = dedupKey,
                Explanation = stale
                    ? $"Last observation is stale by {(evaluationTime - latest.Sample.Timestamp).TotalSeconds:0} seconds."
                    : "Last observation is within the staleness window."
            });
        }

        return new ScenarioPreviewResponse
        {
            Matches = matches,
            GroupCounts = counts,
            DedupKeys = dedup.ToList()
        };
    }

    private static bool CompareAggregate(int count, string operation, double threshold) =>
        operation.ToLowerInvariant() switch
        {
            "gt" => count > threshold,
            "gte" => count >= threshold,
            "lt" => count < threshold,
            "lte" => count <= threshold,
            "eq" => Math.Abs(count - threshold) < double.Epsilon,
            "neq" => Math.Abs(count - threshold) >= double.Epsilon,
            _ => false
        };

    private static ScenarioPreviewResponse PreviewGraph(
        ScenarioDefinition definition,
        string? scenarioId,
        ScenarioPreviewRequest request)
    {
        var executor = new ScenarioGraphExecutor(
            new MngAlarm.Infrastructure.State.InMemoryCorrelationWindowStore(),
            new MngAlarm.Infrastructure.State.InMemorySequenceStateStore());
        var rule = new AlarmRuleDocument
        {
            Id = scenarioId ?? "preview",
            ScenarioId = scenarioId ?? "preview",
            Definition = definition
        };
        var matches = new List<ScenarioPreviewMatch>();
        var traces = new List<ScenarioPreviewNodeTrace>();
        var debugLines = new List<ScenarioPreviewDebugLine>();
        var dedup = new HashSet<string>(StringComparer.Ordinal);
        DateTime? nextEvaluationAt = null;
        var labels = definition.Graph?.Nodes.ToDictionary(
            x => x.Id,
            x => string.IsNullOrWhiteSpace(x.Layout?.Label) ? x.Id : x.Layout!.Label!,
            StringComparer.Ordinal) ?? new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < request.Samples!.Count; i++)
        {
            var sample = request.Samples[i];
            var observation = new ObservationEnvelope
            {
                DomainId = "preview",
                DomainName = "preview",
                Kind = sample.Kind,
                Key = sample.Key,
                Value = sample.Value,
                Dimensions = ObservationValueNormalizer.NormalizeDimensions(sample.Dimensions),
                Timestamp = sample.Timestamp
            };
            var execution = executor.Execute(rule, observation);
            foreach (var output in execution.Outputs) dedup.Add(output.DedupKey);
            nextEvaluationAt = execution.NextEvaluationAt ?? nextEvaluationAt;
            matches.Add(new ScenarioPreviewMatch
            {
                SampleIndex = i,
                Matched = execution.Outputs.Count > 0,
                Explanation = execution.Outputs.Count > 0
                    ? $"Matched outputs: {string.Join(", ", execution.Outputs.Select(x => x.OutputNodeId))}."
                    : "No alarm output reached.",
                DedupKey = execution.Outputs.FirstOrDefault()?.DedupKey ?? string.Empty,
                GroupKey = execution.Outputs.FirstOrDefault()?.GroupKey ?? "_all"
            });
            traces.AddRange(execution.Traces.Select(x => new ScenarioPreviewNodeTrace
            {
                SampleIndex = i,
                NodeId = x.NodeId,
                NodeType = x.NodeType,
                Status = x.Status,
                Outcome = x.Outcome,
                NextEvaluationAt = x.NextEvaluationAt
            }));
            debugLines.AddRange(execution.DebugLines.Select(x => new ScenarioPreviewDebugLine
            {
                SampleIndex = i,
                NodeId = x.NodeId,
                Label = labels.GetValueOrDefault(x.NodeId, x.NodeId),
                Mode = x.Mode,
                Path = x.Path,
                Payload = x.Payload,
                At = x.At
            }));
        }
        return new ScenarioPreviewResponse
        {
            Matches = matches,
            DedupKeys = dedup.ToList(),
            NodeTrace = traces,
            DebugLines = debugLines,
            ExecutionOrder = ScenarioCompiler.CompileGraph(definition).TopologicalOrder,
            NextEvaluationAt = nextEvaluationAt
        };
    }
}
