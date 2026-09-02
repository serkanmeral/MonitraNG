using MngDocument.Application.Contracts.Resources;

namespace MngDocument.Application.Contracts.ResourceLinks;

public sealed class CreateResourceLinkRequest
{
    public required string ResourceId { get; init; }
    public required string TargetModule { get; init; }
    public required string TargetType { get; init; }
    public required string TargetId { get; init; }
    public required string RelationType { get; init; }
}

public sealed class ResourceLinkDto
{
    public required string Id { get; init; }
    public required string ResourceId { get; init; }
    public required string TargetModule { get; init; }
    public required string TargetType { get; init; }
    public required string TargetId { get; init; }
    public required string RelationType { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime? CreatedAt { get; init; }
}

public sealed class LinkedWorkItemSummaryDto
{
    public required string LinkId { get; init; }
    public required string WorkItemId { get; init; }
    public string? WorkItemKey { get; init; }
    public string? WorkItemTitle { get; init; }
    public string? BoardId { get; init; }
    public string? WorkspaceId { get; init; }
    public required string RelationType { get; init; }
}

public sealed class LinkedResourceSummaryDto
{
    public required string LinkId { get; init; }
    public required string ResourceId { get; init; }
    public required string RelationType { get; init; }
    public string? Direction { get; init; }
    public string? Kind { get; init; }
    public string? ResourceType { get; init; }
    public string? Name { get; init; }
    public string? Title { get; init; }
    public string? MimeType { get; init; }
    public string? Extension { get; init; }
    public EffectivePermissionDto Permissions { get; init; } = EffectivePermissionDto.Full;
}

public sealed class ResourceLinkListResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Total { get; init; }
}
