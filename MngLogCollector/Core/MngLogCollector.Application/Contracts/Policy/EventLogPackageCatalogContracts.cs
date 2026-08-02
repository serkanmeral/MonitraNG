namespace MngLogCollector.Application.Contracts.Policy;

public sealed class EventLogPackageDto
{
    public string Name { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public List<int> EventIds { get; set; } = [];

    /// <summary>
    /// Fleet default when true. Host-assigned optional promoted into <c>packages</c> when false.
    /// Always false for items in <c>optionalPackages</c>.
    /// </summary>
    public bool IsDefault { get; set; }
}

public sealed class EventLogPackageCatalogResponse
{
    public string Version { get; set; } = string.Empty;
    public string Source { get; set; } = "collector";
    public DateTime GeneratedUtc { get; set; }
    public List<EventLogPackageDto> Packages { get; set; } = [];
    public List<EventLogPackageDto> OptionalPackages { get; set; } = [];
}

public sealed class EventLogPackageManageItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public List<int> EventIds { get; set; } = [];
    public bool IsDefault { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class EventLogPackageManageListResponse
{
    public string Version { get; set; } = string.Empty;
    public DateTime? PublishedUtc { get; set; }
    public bool HasUnpublishedChanges { get; set; }
    public List<EventLogPackageManageItemDto> Items { get; set; } = [];
}

public sealed class EventLogPackageUpsertRequest
{
    public string Name { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public List<int> EventIds { get; set; } = [];
    public bool IsDefault { get; set; }
}

public sealed class EventLogChannelDictionaryDto
{
    public string Channel { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public List<EventLogKnownIdDto> KnownEventIds { get; set; } = [];
}

public sealed class EventLogKnownIdDto
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
}

/// <summary>Starter template that prefills the create package form (not auto-saved).</summary>
public sealed class EventLogPackagePresetDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string SuggestedName { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public List<int> EventIds { get; set; } = [];
}

public sealed class EventLogHostAssignmentDto
{
    public string Hostname { get; set; } = string.Empty;
    public string HostKey { get; set; } = string.Empty;
    public List<string> EnabledOptionalPackages { get; set; } = [];
    public List<string> DisabledServerPackages { get; set; } = [];
    public DateTime? UpdatedAtUtc { get; set; }
}

public sealed class EventLogHostAssignmentUpsertRequest
{
    public List<string> EnabledOptionalPackages { get; set; } = [];
    public List<string> DisabledServerPackages { get; set; } = [];
}
