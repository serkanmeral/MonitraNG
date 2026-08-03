using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MngReactor.Application.Models.SecEvents;

/// <summary>Editable sec_event parse rule (P5 catalog).</summary>
public sealed class SecEventParseRuleDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    /// <summary>Stable rule key (unique), written to parser.id on match.</summary>
    public string RuleId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool Enabled { get; set; } = true;

    public int Priority { get; set; } = 100;

    public bool Builtin { get; set; }

    public int Version { get; set; } = 1;

    public SecEventParseRuleMatch Match { get; set; } = new();

    public List<SecEventParseRuleExtractStep> Extract { get; set; } = [];

    /// <summary>v1: first_wins only.</summary>
    public string OnConflict { get; set; } = "first_wins";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Domain-defined custom.* target field (parse + future smart query).</summary>
public sealed class SecEventCustomFieldDocument
{
    [BsonId]
    public string Name { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string ValueType { get; set; } = "keyword";

    public string? Description { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class SecEventParseRuleMatch
{
    public List<string> SourceProduct { get; set; } = [];

    public List<string>? SourceType { get; set; }

    public List<string>? Channel { get; set; }

    public List<int>? EventIds { get; set; }

    public List<SecEventParseRuleWhen>? When { get; set; }

    public List<SecEventParseRuleMessagePattern>? MessagePatterns { get; set; }
}

public sealed class SecEventParseRuleWhen
{
    public string Field { get; set; } = string.Empty;

    /// <summary>eq | neq | in | exists</summary>
    public string Op { get; set; } = "eq";

    public string? Value { get; set; }

    public List<string>? Values { get; set; }
}

public sealed class SecEventParseRuleMessagePattern
{
    public string Family { get; set; } = string.Empty;
}

public sealed class SecEventParseRuleExtractStep
{
    /// <summary>event_data | json_path | regex | kv | constant</summary>
    public string Type { get; set; } = string.Empty;

    public string? From { get; set; }

    public string? To { get; set; }

    public string? Value { get; set; }

    public string? Pattern { get; set; }

    /// <summary>Regex group index/name → target field.</summary>
    public Dictionary<string, string>? Groups { get; set; }
}

/// <summary>Singleton meta for published catalog version.</summary>
public sealed class SecEventParseCatalogMetaDocument
{
    public const string SingletonId = "meta";

    [BsonId]
    public string Id { get; set; } = SingletonId;

    public string Version { get; set; } = "0";

    public DateTime PublishedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Last applied builtin seed revision (see SecEventParseRuleCatalogSeed.SeedRevision).</summary>
    public int BuiltinSeedRevision { get; set; }
}
