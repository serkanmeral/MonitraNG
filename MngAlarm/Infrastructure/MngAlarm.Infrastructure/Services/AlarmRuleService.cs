using MngAlarm.Application.Contracts;
using MngAlarm.Application.Services;
using MngAlarm.Domain.Constants;
using MngAlarm.Domain.Entities;

namespace MngAlarm.Infrastructure.Services;

public sealed class AlarmRuleService(IAlarmDomainAccessor domain, IAlarmRuleRepository rules) : IAlarmRuleService
{
    public async Task<AlarmRuleDocument> CreateAsync(CreateAlarmRuleRequest request, CancellationToken cancellationToken = default)
    {
        var ctx = domain.GetRequiredDomain();
        var now = DateTime.UtcNow;

        var rule = new AlarmRuleDocument
        {
            DomainId = ctx.DomainId,
            DomainName = ctx.DomainName,
            Name = request.Name,
            Type = string.IsNullOrWhiteSpace(request.Type) ? "threshold" : request.Type.Trim(),
            MatchKey = request.MatchKey,
            Operator = request.Operator,
            Threshold = request.Threshold,
            Severity = request.Severity,
            CooldownMinutes = request.CooldownMinutes,
            GroupByFields = request.GroupByFields ?? [],
            WindowMinutes = request.WindowMinutes > 0 ? request.WindowMinutes : 5,
            StalenessMinutes = request.StalenessMinutes,
            DedupKeyTemplate = string.IsNullOrWhiteSpace(request.DedupKeyTemplate)
                ? DefaultDedupTemplate(request.Type)
                : request.DedupKeyTemplate.Trim(),
            SequenceSteps = MapSequenceSteps(request.SequenceSteps),
            Metadata = MapMetadata(request.Metadata),
            CreatedAt = now,
            UpdatedAt = now
        };

        await rules.InsertAsync(rule, cancellationToken);
        return rule;
    }

    private static string DefaultDedupTemplate(string? type) =>
        string.Equals(type, AlarmRuleTypes.Correlation, StringComparison.Ordinal)
        || string.Equals(type, AlarmRuleTypes.Scheduled, StringComparison.Ordinal)
        || string.Equals(type, AlarmRuleTypes.Sequence, StringComparison.Ordinal)
            ? "{ruleId}:{groupKey}"
            : "{ruleId}:{key}";

    private static AlarmRuleMetadata? MapMetadata(AlarmRuleMetadataDto? metadata)
    {
        if (metadata == null || string.IsNullOrWhiteSpace(metadata.PackageId))
            return null;

        return new AlarmRuleMetadata
        {
            PackageId = metadata.PackageId.Trim(),
            PackageVersion = metadata.PackageVersion?.Trim() ?? string.Empty,
            ScenarioId = metadata.ScenarioId?.Trim() ?? string.Empty,
            Description = metadata.Description?.Trim() ?? string.Empty,
            ThreatTacticId = metadata.ThreatTacticId?.Trim() ?? string.Empty,
            ThreatTacticName = metadata.ThreatTacticName?.Trim() ?? string.Empty,
            ThreatTechniqueId = metadata.ThreatTechniqueId?.Trim() ?? string.Empty,
            ThreatTechniqueName = metadata.ThreatTechniqueName?.Trim() ?? string.Empty,
            ComplianceTags = metadata.ComplianceTags?
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? []
        };
    }

    private static List<AlarmSequenceStep> MapSequenceSteps(List<AlarmSequenceStepDto>? steps)
    {
        if (steps == null || steps.Count == 0)
            return [];

        return steps
            .Where(s => !string.IsNullOrWhiteSpace(s.MatchKey))
            .Select(s => new AlarmSequenceStep
            {
                MatchKey = s.MatchKey.Trim(),
                MinCount = Math.Max(1, s.MinCount),
                WithinMinutes = s.WithinMinutes,
                WithinMinutesAfterFirst = s.WithinMinutesAfterFirst
            })
            .ToList();
    }

    public Task<IReadOnlyList<AlarmRuleDocument>> ListAsync(CancellationToken cancellationToken = default)
    {
        var ctx = domain.GetRequiredDomain();
        return rules.ListAllAsync(ctx.DomainName, cancellationToken);
    }

    public async Task<AlarmRuleDocument?> GetAsync(string ruleId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ruleId))
            return null;

        var ctx = domain.GetRequiredDomain();
        return await rules.GetByIdAsync(ctx.DomainName, ruleId.Trim(), cancellationToken);
    }

    public async Task<AlarmRuleDocument?> UpdateAsync(
        string ruleId,
        UpdateAlarmRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        var ctx = domain.GetRequiredDomain();
        var existing = await rules.GetByIdAsync(ctx.DomainName, ruleId.Trim(), cancellationToken);
        if (existing == null)
            return null;

        if (!string.IsNullOrWhiteSpace(request.Name))
            existing.Name = request.Name.Trim();
        if (request.Enabled.HasValue)
            existing.Enabled = request.Enabled.Value;
        if (request.Severity.HasValue)
            existing.Severity = request.Severity.Value;
        if (!string.IsNullOrWhiteSpace(request.Operator))
            existing.Operator = request.Operator.Trim();
        if (request.Threshold.HasValue)
            existing.Threshold = request.Threshold.Value;
        if (request.CooldownMinutes.HasValue)
            existing.CooldownMinutes = request.CooldownMinutes.Value;
        if (request.GroupByFields != null)
            existing.GroupByFields = request.GroupByFields;
        if (request.WindowMinutes.HasValue)
            existing.WindowMinutes = request.WindowMinutes.Value;
        if (request.StalenessMinutes.HasValue)
            existing.StalenessMinutes = request.StalenessMinutes.Value;
        if (!string.IsNullOrWhiteSpace(request.DedupKeyTemplate))
            existing.DedupKeyTemplate = request.DedupKeyTemplate.Trim();

        existing.UpdatedAt = DateTime.UtcNow;
        await rules.UpdateAsync(existing, cancellationToken);
        return existing;
    }

    public async Task<bool> DeleteAsync(string ruleId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ruleId))
            return false;

        var ctx = domain.GetRequiredDomain();
        var existing = await rules.GetByIdAsync(ctx.DomainName, ruleId.Trim(), cancellationToken);
        if (existing == null)
            return false;

        await rules.DeleteAsync(ctx.DomainName, existing.Id, cancellationToken);
        return true;
    }
}
