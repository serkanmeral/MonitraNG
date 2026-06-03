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
}

public sealed class UpdateAlarmRuleRequest
{
    public string? Name { get; set; }
    public bool? Enabled { get; set; }
    public int? Severity { get; set; }
    public string? Operator { get; set; }
    public double? Threshold { get; set; }
    public int? CooldownMinutes { get; set; }
    public List<string>? GroupByFields { get; set; }
    public int? WindowMinutes { get; set; }
    public int? StalenessMinutes { get; set; }
    public string? DedupKeyTemplate { get; set; }
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

public sealed record AlarmDomainContext(string DomainId, string DomainName);
