using MngAlarm.Application.Observations;
using MngAlarm.Domain.Entities;

namespace MngAlarm.Application.Contracts;

public sealed class CreateAlarmRuleRequest
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "threshold";
    public int Severity { get; set; } = 5;
    public string MatchKey { get; set; } = string.Empty;
    public string Operator { get; set; } = "gt";
    public double Threshold { get; set; }
    public int CooldownMinutes { get; set; } = 5;
    public List<string>? GroupByFields { get; set; }
    public int WindowMinutes { get; set; } = 5;
    public int StalenessMinutes { get; set; }
    public string? DedupKeyTemplate { get; set; }
    public List<AlarmSequenceStepDto>? SequenceSteps { get; set; }
    public AlarmRuleMetadataDto? Metadata { get; set; }
    public ScenarioDefinition? Definition { get; set; }
}

public sealed class AlarmRuleMetadataDto
{
    public string PackageId { get; set; } = string.Empty;
    public string PackageVersion { get; set; } = string.Empty;
    public string ScenarioId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ThreatTacticId { get; set; } = string.Empty;
    public string ThreatTacticName { get; set; } = string.Empty;
    public string ThreatTechniqueId { get; set; } = string.Empty;
    public string ThreatTechniqueName { get; set; } = string.Empty;
    public List<string>? ComplianceTags { get; set; }
}

public sealed class AlarmSequenceStepDto
{
    public string MatchKey { get; set; } = string.Empty;
    public int MinCount { get; set; } = 1;
    public int WithinMinutes { get; set; }
    public int WithinMinutesAfterFirst { get; set; }
}

public sealed class UpdateAlarmRuleRequest
{
    public string? Name { get; set; }
    public bool? Enabled { get; set; }
    public string? Type { get; set; }
    public string? MatchKey { get; set; }
    public int? Severity { get; set; }
    public string? Operator { get; set; }
    public double? Threshold { get; set; }
    public int? CooldownMinutes { get; set; }
    public List<string>? GroupByFields { get; set; }
    public int? WindowMinutes { get; set; }
    public int? StalenessMinutes { get; set; }
    public string? DedupKeyTemplate { get; set; }
    public List<AlarmSequenceStepDto>? SequenceSteps { get; set; }
    public AlarmRuleMetadataDto? Metadata { get; set; }
    public ScenarioDefinition? Definition { get; set; }
}

public sealed class CreateScenarioDraftRequest
{
    public string Name { get; set; } = string.Empty;
    public int Severity { get; set; } = 5;
    public bool Enabled { get; set; }
    public ScenarioDefinition Definition { get; set; } = new();
}

public sealed class UpdateScenarioDraftRequest
{
    public string? Name { get; set; }
    public int? Severity { get; set; }
    public bool? Enabled { get; set; }
    public ScenarioDefinition? Definition { get; set; }
}

public sealed class ScenarioPreviewRequest
{
    public ScenarioDefinition? Definition { get; set; }
    public List<ScenarioSampleObservation>? Samples { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}

public sealed class ScenarioSampleObservation
{
    public string Kind { get; set; } = "event";
    public string Key { get; set; } = string.Empty;
    public double? Value { get; set; }
    public Dictionary<string, object?> Dimensions { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public sealed class ScenarioPreviewResponse
{
    public bool Supported { get; init; } = true;
    public IReadOnlyList<ScenarioDiagnostic> Diagnostics { get; init; } = [];
    public IReadOnlyList<ScenarioPreviewMatch> Matches { get; init; } = [];
    public IReadOnlyDictionary<string, int> GroupCounts { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<string> DedupKeys { get; init; } = [];
}

public sealed class ScenarioPreviewMatch
{
    public int SampleIndex { get; init; }
    public bool Matched { get; init; }
    public string Explanation { get; init; } = string.Empty;
    public string GroupKey { get; init; } = "_all";
    public string DedupKey { get; init; } = string.Empty;
}

public sealed class ScenarioCatalogItem
{
    public string ScenarioId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int LatestVersion { get; init; }
    public string LatestStatus { get; init; } = string.Empty;
    public int? PublishedVersion { get; init; }
    public int? DraftVersion { get; init; }
    public bool Enabled { get; init; }
    public int Severity { get; init; }
    public string Origin { get; init; } = ScenarioOrigins.User;
    public bool IsReadOnly { get; init; }
    public string? TemplateId { get; init; }
    public string? PackageId { get; init; }
    public string? PackageVersion { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class ImportScenarioPackageRequest
{
    public string PackageId { get; set; } = string.Empty;
    public string PackageVersion { get; set; } = string.Empty;
    public List<ImportScenarioTemplateRequest> Templates { get; set; } = [];
}

public sealed class ImportScenarioTemplateRequest
{
    public string TemplateId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Severity { get; set; } = 5;
    public ScenarioDefinition Definition { get; set; } = new();
}

public sealed class ScenarioPackageImportResult
{
    public int Created { get; init; }
    public int Skipped { get; init; }
    public IReadOnlyList<string> ScenarioIds { get; init; } = [];
}

public sealed class ScenarioScheduleTriggerRequest
{
    public List<ScenarioSampleObservation>? Samples { get; set; }
    public DateTime? EvaluationTime { get; set; }
}

public sealed class ScenarioScheduleTriggerResult
{
    public bool Supported { get; init; }
    public string? DiagnosticCode { get; init; }
    public int ObservationsProcessed { get; init; }
    public int AlarmsRaised { get; init; }
}

public sealed class AlarmValidationScanResponse
{
    public int CorrelationResolved { get; init; }
    public int ScheduledRaised { get; init; }
    public int ScheduledResolved { get; init; }
    public int WindowsPruned { get; init; }
}

public sealed class IngestObservationRequest
{
    public string? DomainName { get; set; }
    public string? DomainId { get; set; }
    public string Kind { get; set; } = "metric";
    public required string Key { get; set; }
    public double? Value { get; set; }
    public Dictionary<string, object?>? Dimensions { get; set; }
}

public sealed class AlarmProcessResult
{
    public int RulesEvaluated { get; init; }
    public int AlarmsRaised { get; init; }
    public int AlarmsUpdated { get; init; }
    public int AlarmsResolved { get; init; }
    public IReadOnlyList<string> AlarmIds { get; init; } = Array.Empty<string>();
}

public sealed class AlarmSummaryDto
{
    public required string Id { get; init; }
    public required string RuleId { get; init; }
    public required string DedupKey { get; init; }
    public required string DomainId { get; init; }
    public required string DomainName { get; init; }
    public int Severity { get; init; }
    public required string Status { get; init; }
    public DateTime FirstSeenAt { get; init; }
    public DateTime LastSeenAt { get; init; }
    public int Count { get; init; }
    public string CorrelationId { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, object?> Context { get; init; } = new Dictionary<string, object?>();
}

public sealed class AlarmListResponse
{
    public required IReadOnlyList<AlarmSummaryDto> Items { get; init; }
    public long Total { get; init; }
    public int Skip { get; init; }
    public int Limit { get; init; }
}

public sealed class AlarmScenarioRollupDto
{
    public required string MatchKey { get; init; }
    public int OpenCount { get; init; }
    public int TotalInRange { get; init; }
    public int? MaxSeverity { get; init; }
    public DateTime? LastSeenAt { get; init; }
}

public sealed class AlarmDashboardSnapshot
{
    public required DateTime From { get; init; }
    public required DateTime To { get; init; }
    public long OpenTotal { get; init; }
    public required IReadOnlyList<AlarmSummaryDto> OpenAlarms { get; init; }
    public required IReadOnlyList<AlarmScenarioRollupDto> ScenarioRollup { get; init; }
}

public sealed class AlarmTrendBucketDto
{
    public required DateTime Bucket { get; init; }
    public int Count { get; init; }
}

public sealed class AlarmTrendBucketsResult
{
    public required DateTime From { get; init; }
    public required DateTime To { get; init; }
    public required IReadOnlyList<AlarmTrendBucketDto> Items { get; init; }
}

public sealed record AlarmDomainContext(string DomainId, string DomainName);
