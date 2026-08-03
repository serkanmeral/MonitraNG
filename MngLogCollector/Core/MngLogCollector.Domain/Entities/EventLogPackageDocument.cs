using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MngLogCollector.Domain.Entities;

/// <summary>Editable Windows Event Log package definition (single channel).</summary>
public sealed class EventLogPackageDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    /// <summary>Stable package key (unique), e.g. system-lifecycle.</summary>
    public string Name { get; set; } = string.Empty;

    public string Channel { get; set; } = string.Empty;

    /// <summary>
    /// <c>selected</c> = collect listed <see cref="EventIds"/>;
    /// <c>all</c> = collect entire channel except optional <see cref="ExcludedEventIds"/>.
    /// Missing/legacy documents are treated as <c>selected</c>.
    /// </summary>
    public string SelectionMode { get; set; } = "selected";

    /// <summary>Include list when <see cref="SelectionMode"/> is <c>selected</c>.</summary>
    public List<int> EventIds { get; set; } = [];

    /// <summary>Exclude list when <see cref="SelectionMode"/> is <c>all</c>.</summary>
    public List<int> ExcludedEventIds { get; set; } = [];

    /// <summary>When true, included in agent <c>packages</c> (fleet defaults).</summary>
    public bool IsDefault { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Singleton meta doc for published catalog version (ETag).</summary>
public sealed class EventLogCatalogMetaDocument
{
    public const string SingletonId = "meta";

    [BsonId]
    public string Id { get; set; } = SingletonId;

    public string Version { get; set; } = "0";

    public DateTime PublishedUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Per-host Event Log package plan (optional enable + default disable).
/// <see cref="HostKey"/> is normalized short hostname (lower, no domain).
/// </summary>
public sealed class EventLogHostAssignmentDocument
{
    [BsonId]
    public string HostKey { get; set; } = string.Empty;

    /// <summary>Optional catalog package names promoted into this host's effective packages.</summary>
    public List<string> EnabledOptionalPackages { get; set; } = [];

    /// <summary>Fleet-default package names excluded for this host.</summary>
    public List<string> DisabledServerPackages { get; set; } = [];

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
