namespace MngReactor.Application.Contracts.SecEvents;

public sealed class SecEventParseRuleMatchDto
{
    public List<string> SourceProduct { get; set; } = [];
    public List<string>? SourceType { get; set; }
    public List<string>? Channel { get; set; }
    public List<int>? EventIds { get; set; }
    public List<SecEventParseRuleWhenDto>? When { get; set; }
    public List<SecEventParseRuleMessagePatternDto>? MessagePatterns { get; set; }
}

public sealed class SecEventParseRuleWhenDto
{
    public string Field { get; set; } = string.Empty;
    public string Op { get; set; } = "eq";
    public string? Value { get; set; }
    public List<string>? Values { get; set; }
}

public sealed class SecEventParseRuleMessagePatternDto
{
    public string Family { get; set; } = string.Empty;
}

public sealed class SecEventParseRuleExtractStepDto
{
    public string Type { get; set; } = string.Empty;
    public string? From { get; set; }
    public string? To { get; set; }
    public string? Value { get; set; }
    public string? Pattern { get; set; }
    public Dictionary<string, string>? Groups { get; set; }
}

public sealed class SecEventParseRuleUpsertRequest
{
    public string RuleId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; } = 100;
    public SecEventParseRuleMatchDto Match { get; set; } = new();
    public List<SecEventParseRuleExtractStepDto> Extract { get; set; } = [];
    public string OnConflict { get; set; } = "first_wins";
}

public sealed class SecEventParseRuleManageItemDto
{
    public string Id { get; set; } = string.Empty;
    public string RuleId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Enabled { get; set; }
    public int Priority { get; set; }
    public bool Builtin { get; set; }
    public int Version { get; set; }
    public SecEventParseRuleMatchDto Match { get; set; } = new();
    public List<SecEventParseRuleExtractStepDto> Extract { get; set; } = [];
    public string OnConflict { get; set; } = "first_wins";
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class SecEventParseRuleManageListResponse
{
    public string Version { get; set; } = string.Empty;
    public DateTime? PublishedUtc { get; set; }
    public bool HasUnpublishedChanges { get; set; }
    public List<SecEventParseRuleManageItemDto> Items { get; set; } = [];
}

public sealed class SecEventParseRulePublishedResponse
{
    public string Version { get; set; } = string.Empty;
    public DateTime? PublishedUtc { get; set; }
    public List<SecEventParseRuleManageItemDto> Rules { get; set; } = [];
}

public sealed class SecEventParseRulePreviewRequest
{
    /// <summary>When set, only this rule is applied; otherwise first matching published/enabled rule.</summary>
    public string? RuleId { get; set; }

    /// <summary>
    /// Unsaved draft rule (wizard / editor). When set, preview uses this document without persisting.
    /// Takes precedence over <see cref="RuleId"/>.
    /// </summary>
    public SecEventParseRuleUpsertRequest? DraftRule { get; set; }

    public SecEventParseRulePreviewContext Context { get; set; } = new();
}

public sealed class SecEventWindowsParseSampleRequest
{
    public string? Channel { get; set; }
    public int? EventId { get; set; }
    public string? Host { get; set; }
    public int Limit { get; set; } = 1;
    public int Hours { get; set; } = 168;
}

public sealed class SecEventWindowsParseSampleDto
{
    public string Id { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Host { get; set; }
    public string? Channel { get; set; }
    public int? EventId { get; set; }
    public string? Provider { get; set; }
    public string? Package { get; set; }
    public string? Message { get; set; }
    public string? EventDataText { get; set; }
    public Dictionary<string, string> EventData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string ParseModeHint { get; set; } = "text";
    /// <summary>Canonical raw object for preview (fields flattened to root).</summary>
    public object? Raw { get; set; }
    public string? SourceType { get; set; }
    public string? SourceProduct { get; set; }
}

public sealed class SecEventWindowsParseSampleResponse
{
    public List<SecEventWindowsParseSampleDto> Items { get; set; } = [];
    public List<int> RecentEventIds { get; set; } = [];

    /// <summary>Lookback window actually used (hours).</summary>
    public int Hours { get; set; }

    /// <summary>OpenSearch hits.total for the sample query (when available).</summary>
    public long TotalHits { get; set; }

    /// <summary>Host filter after IP→hostname resolution (when applied).</summary>
    public string? EffectiveHost { get; set; }

    /// <summary>Human-readable diagnostics (empty result, host resolution, etc.).</summary>
    public List<string> Notes { get; set; } = [];
}

public sealed class SecEventLinuxParseSampleRequest
{
    /// <summary>Journal package filter (sshd, sudo, unit-fail, …).</summary>
    public string? Package { get; set; }

    /// <summary>Optional free-text query against message / rawPreview.</summary>
    public string? Query { get; set; }

    public string? Host { get; set; }
    public int Limit { get; set; } = 1;
    public int Hours { get; set; } = 168;
}

public sealed class SecEventLinuxParseSampleDto
{
    public string Id { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string? Host { get; set; }
    public string? Package { get; set; }
    public string? Unit { get; set; }
    public string? Channel { get; set; }
    public string? Message { get; set; }
    public string? EventAction { get; set; }
    /// <summary>Structured journal / fields bag for optional direct maps.</summary>
    public Dictionary<string, string> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public object? Raw { get; set; }
    public string? SourceType { get; set; }
    public string? SourceProduct { get; set; }
}

public sealed class SecEventLinuxParseSampleResponse
{
    public List<SecEventLinuxParseSampleDto> Items { get; set; } = [];
    public List<string> RecentPackages { get; set; } = [];
    public int Hours { get; set; }
    public long TotalHits { get; set; }
    public string? EffectiveHost { get; set; }
    public List<string> Notes { get; set; } = [];
}

public sealed class SecEventParseRulePreviewContext
{
    public SecEventParseRulePreviewSource? Source { get; set; }

    /// <summary>Structured raw (Windows Event JSON) and/or message text.</summary>
    public object? Raw { get; set; }

    public string? Message { get; set; }

    public string? Channel { get; set; }

    public int? EventId { get; set; }
}

public sealed class SecEventParseRulePreviewSource
{
    public string? Product { get; set; }
    public string? Type { get; set; }
    public string? Host { get; set; }
}

public sealed class SecEventParseRulePreviewResponse
{
    public bool Matched { get; set; }
    public string? RuleId { get; set; }
    public Dictionary<string, object?> Fields { get; set; } = new(StringComparer.Ordinal);
    public List<string> Notes { get; set; } = [];
}

/// <summary>Shared field catalog for parse extract targets and future smart queries.</summary>
public sealed class SecEventTargetFieldDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    /// <summary>actor | network | event | message | tags | custom</summary>
    public string Group { get; set; } = string.Empty;
    /// <summary>keyword | ip | port | text</summary>
    public string ValueType { get; set; } = "keyword";
    public string? Description { get; set; }
    /// <summary>Extract step types allowed to write this field.</summary>
    public List<string> ExtractTypes { get; set; } = [];
    /// <summary>Suggested operators for future smart query UI.</summary>
    public List<string> QueryOperators { get; set; } = [];
    public bool Queryable { get; set; } = true;
    /// <summary>Shown in parse wizard field-mapping dropdown (event.action is set separately).</summary>
    public bool WizardSelectable { get; set; } = true;
    /// <summary>Domain-defined custom.* extension (not part of the core schema).</summary>
    public bool IsCustom { get; set; }
}

public sealed class SecEventTargetFieldCatalogResponse
{
    public string Version { get; set; } = "1";
    public List<SecEventTargetFieldDefinition> Fields { get; set; } = [];
}

public sealed class SecEventCustomFieldUpsertRequest
{
    /// <summary>Full name (custom.session_id) or bare slug (session_id).</summary>
    public string Name { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string? ValueType { get; set; }
    public string? Description { get; set; }
}
