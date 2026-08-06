using MongoDB.Bson.Serialization.Attributes;

namespace MngAlarm.Domain.Entities;

public static class ScenarioLifecycleStatuses
{
    public const string Draft = "draft";
    public const string Validated = "validated";
    public const string Published = "published";
    public const string Archived = "archived";
}

public static class ScenarioSourceKinds
{
    public const string Observation = "observation";
    public const string ScheduledStaleness = "scheduled-staleness";
    public const string ScheduledQuery = "scheduled-query";
    public const string MetaCorrelation = "meta-correlation";
}

public static class ScenarioOrigins
{
    public const string User = "user";
    public const string Product = "product";
}

public static class ScenarioNodeTypes
{
    public const string Source = "source";
    public const string Condition = "condition";
    public const string Filter = "filter";
    public const string Aggregation = "aggregation";
    public const string Threshold = "threshold";
    public const string Sequence = "sequence";
    public const string Decision = "decision";
    public const string AlarmOutput = "alarm-output";
    public const string StopOutput = "stop-output";
    public const string DebugOutput = "debug-output";
}

[BsonIgnoreExtraElements]
public sealed class ScenarioGraph
{
    [BsonElement("nodes")]
    public List<ScenarioNode> Nodes { get; set; } = [];

    [BsonElement("edges")]
    public List<ScenarioEdge> Edges { get; set; } = [];
}

[BsonIgnoreExtraElements]
public sealed class ScenarioNode
{
    [BsonElement("id")]
    public string Id { get; set; } = string.Empty;

    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;

    [BsonElement("config")]
    public ScenarioNodeConfig Config { get; set; } = new();

    [BsonElement("layout")]
    [BsonIgnoreIfNull]
    public ScenarioNodeLayout? Layout { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class ScenarioNodeConfig
{
    [BsonElement("source")]
    [BsonIgnoreIfNull]
    public ScenarioSource? Source { get; set; }

    [BsonElement("condition")]
    [BsonIgnoreIfNull]
    public ScenarioCondition? Condition { get; set; }

    [BsonElement("aggregation")]
    [BsonIgnoreIfNull]
    public ScenarioAggregation? Aggregation { get; set; }

    [BsonElement("window")]
    [BsonIgnoreIfNull]
    public ScenarioWindow? Window { get; set; }

    [BsonElement("sequence")]
    [BsonIgnoreIfNull]
    public ScenarioSequence? Sequence { get; set; }

    [BsonElement("groupBy")]
    public List<string> GroupBy { get; set; } = [];

    [BsonElement("dedup")]
    [BsonIgnoreIfNull]
    public ScenarioDedup? Dedup { get; set; }

    [BsonElement("severity")]
    [BsonIgnoreIfNull]
    public int? Severity { get; set; }

    [BsonElement("settleAfterSeconds")]
    public int SettleAfterSeconds { get; set; }

    [BsonElement("debug")]
    [BsonIgnoreIfNull]
    public ScenarioDebug? Debug { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class ScenarioDebug
{
    /// <summary>complete = observation summary; path = single field from observation root.</summary>
    [BsonElement("mode")]
    public string Mode { get; set; } = "complete";

    /// <summary>Field path when mode is path (e.g. value, key, dimensions.sourceHost).</summary>
    [BsonElement("path")]
    [BsonIgnoreIfNull]
    public string? Path { get; set; }

    /// <summary>When false, simulate still reaches the node but emits no debug line.</summary>
    [BsonElement("active")]
    public bool Active { get; set; } = true;
}

[BsonIgnoreExtraElements]
public sealed class ScenarioNodeLayout
{
    [BsonElement("x")]
    public double X { get; set; }

    [BsonElement("y")]
    public double Y { get; set; }

    [BsonElement("label")]
    [BsonIgnoreIfNull]
    public string? Label { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class ScenarioEdge
{
    [BsonElement("id")]
    public string Id { get; set; } = string.Empty;

    [BsonElement("from")]
    public string From { get; set; } = string.Empty;

    [BsonElement("to")]
    public string To { get; set; } = string.Empty;

    [BsonElement("fromPort")]
    public string FromPort { get; set; } = "next";

    [BsonElement("toPort")]
    public string ToPort { get; set; } = "in";
}

[BsonIgnoreExtraElements]
public sealed class ScenarioDefinition
{
    [BsonElement("schemaVersion")]
    public int SchemaVersion { get; set; } = 2;

    [BsonElement("source")]
    public ScenarioSource Source { get; set; } = new();

    [BsonElement("condition")]
    public ScenarioCondition? Condition { get; set; }

    [BsonElement("aggregation")]
    public ScenarioAggregation? Aggregation { get; set; }

    [BsonElement("groupBy")]
    public List<string> GroupBy { get; set; } = [];

    [BsonElement("window")]
    public ScenarioWindow? Window { get; set; }

    [BsonElement("sequence")]
    public ScenarioSequence? Sequence { get; set; }

    [BsonElement("dedup")]
    public ScenarioDedup Dedup { get; set; } = new();

    [BsonElement("hysteresis")]
    public ScenarioHysteresis? Hysteresis { get; set; }

    [BsonElement("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.Ordinal);

    [BsonElement("graph")]
    [BsonIgnoreIfNull]
    public ScenarioGraph? Graph { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class ScenarioSource
{
    [BsonElement("kind")]
    public string Kind { get; set; } = ScenarioSourceKinds.Observation;

    [BsonElement("observationKind")]
    public string? ObservationKind { get; set; }

    [BsonElement("matchKey")]
    public string MatchKey { get; set; } = string.Empty;

    /// <summary>
    /// Optional additional observation keys. When non-empty, source matches if
    /// observation.Key equals MatchKey or any MatchKeys entry.
    /// </summary>
    [BsonElement("matchKeys")]
    public List<string> MatchKeys { get; set; } = [];

    /// <summary>Legacy free-form query field. New definitions must leave this empty.</summary>
    [BsonElement("query")]
    public string? Query { get; set; }

    [BsonElement("schedule")]
    public string? Schedule { get; set; }

    [BsonElement("scheduleDefinition")]
    public ScenarioSchedule? ScheduleDefinition { get; set; }

    [BsonElement("dependsOnScenarioIds")]
    public List<string> DependsOnScenarioIds { get; set; } = [];

    [BsonElement("maxChainDepth")]
    public int MaxChainDepth { get; set; } = 5;
}

[BsonIgnoreExtraElements]
public sealed class ScenarioSchedule
{
    [BsonElement("expression")]
    public string Expression { get; set; } = string.Empty;

    [BsonElement("timeZone")]
    public string TimeZone { get; set; } = "UTC";

    [BsonElement("maxLookbackSeconds")]
    public int MaxLookbackSeconds { get; set; } = 3600;
}

[BsonIgnoreExtraElements]
public sealed class ScenarioCondition
{
    [BsonElement("logic")]
    public string? Logic { get; set; }

    [BsonElement("children")]
    public List<ScenarioCondition> Children { get; set; } = [];

    [BsonElement("field")]
    public string? Field { get; set; }

    [BsonElement("operator")]
    public string? Operator { get; set; }

    [BsonElement("value")]
    public object? Value { get; set; }

    [BsonElement("sustainedForSeconds")]
    public int SustainedForSeconds { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class ScenarioAggregation
{
    [BsonElement("function")]
    public string Function { get; set; } = "count";

    [BsonElement("field")]
    public string? Field { get; set; }

    [BsonElement("operator")]
    public string Operator { get; set; } = "gte";

    [BsonElement("threshold")]
    public double Threshold { get; set; } = 1;
}

[BsonIgnoreExtraElements]
public sealed class ScenarioWindow
{
    [BsonElement("durationSeconds")]
    public int DurationSeconds { get; set; } = 300;

    [BsonElement("stalenessSeconds")]
    public int StalenessSeconds { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class ScenarioSequence
{
    [BsonElement("steps")]
    public List<ScenarioSequenceStep> Steps { get; set; } = [];
}

[BsonIgnoreExtraElements]
public sealed class ScenarioSequenceStep
{
    [BsonElement("matchKey")]
    public string MatchKey { get; set; } = string.Empty;

    [BsonElement("condition")]
    public ScenarioCondition? Condition { get; set; }

    [BsonElement("minCount")]
    public int MinCount { get; set; } = 1;

    [BsonElement("withinSeconds")]
    public int WithinSeconds { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class ScenarioDedup
{
    [BsonElement("keyTemplate")]
    public string KeyTemplate { get; set; } = "{ruleId}:{key}";

    [BsonElement("cooldownSeconds")]
    public int CooldownSeconds { get; set; } = 300;
}

[BsonIgnoreExtraElements]
public sealed class ScenarioHysteresis
{
    [BsonElement("raiseThreshold")]
    public double RaiseThreshold { get; set; }

    [BsonElement("clearThreshold")]
    public double ClearThreshold { get; set; }

    [BsonElement("minimumStateSeconds")]
    public int MinimumStateSeconds { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class ScenarioVersionDocument
{
    [BsonId]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [BsonElement("scenarioId")]
    public string ScenarioId { get; set; } = Guid.NewGuid().ToString("N");

    [BsonElement("domainId")]
    public string DomainId { get; set; } = string.Empty;

    [BsonElement("domainName")]
    public string DomainName { get; set; } = string.Empty;

    [BsonElement("version")]
    public int Version { get; set; } = 1;

    [BsonElement("status")]
    public string Status { get; set; } = ScenarioLifecycleStatuses.Draft;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("enabled")]
    public bool Enabled { get; set; }

    [BsonElement("severity")]
    public int Severity { get; set; } = 5;

    [BsonElement("definition")]
    public ScenarioDefinition Definition { get; set; } = new();

    [BsonElement("origin")]
    public string Origin { get; set; } = ScenarioOrigins.User;

    [BsonElement("isReadOnly")]
    public bool IsReadOnly { get; set; }

    [BsonElement("templateId")]
    public string? TemplateId { get; set; }

    [BsonElement("packageId")]
    public string? PackageId { get; set; }

    [BsonElement("packageVersion")]
    public string? PackageVersion { get; set; }

    [BsonElement("validation")]
    public ScenarioValidationSnapshot? Validation { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("publishedAt")]
    public DateTime? PublishedAt { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class ScenarioValidationSnapshot
{
    [BsonElement("isValid")]
    public bool IsValid { get; set; }

    [BsonElement("diagnostics")]
    public List<ScenarioDiagnostic> Diagnostics { get; set; } = [];

    [BsonElement("validatedAt")]
    public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
}

[BsonIgnoreExtraElements]
public sealed class ScenarioDiagnostic
{
    [BsonElement("code")]
    public string Code { get; set; } = string.Empty;

    [BsonElement("message")]
    public string Message { get; set; } = string.Empty;

    [BsonElement("path")]
    public string? Path { get; set; }

    [BsonElement("severity")]
    public string Severity { get; set; } = "error";
}

[BsonIgnoreExtraElements]
public sealed class ScenarioAuditDocument
{
    [BsonId]
    [BsonRepresentation(MongoDB.Bson.BsonType.String)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [BsonElement("scenarioId")]
    public string ScenarioId { get; set; } = string.Empty;

    [BsonElement("domainName")]
    public string DomainName { get; set; } = string.Empty;

    [BsonElement("version")]
    public int Version { get; set; }

    [BsonElement("action")]
    public string Action { get; set; } = string.Empty;

    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
