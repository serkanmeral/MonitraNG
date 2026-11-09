using System.Text.Json.Serialization;

namespace MngOperations.Application.Contracts.Workspaces;

public sealed class VisibleWorkspaceItem
{
    [JsonPropertyName("__dataId")]
    public string DataId { get; init; } = "";

    public string Name { get; init; } = "";

    public string? Description { get; init; }

    [JsonPropertyName("workspaceType")]
    public string? WorkspaceType { get; init; }

    [JsonPropertyName("workItemKeyPrefix")]
    public string? WorkItemKeyPrefix { get; init; }

    [JsonPropertyName("folderId")]
    public string? FolderId { get; init; }

    [JsonPropertyName("diFolderId")]
    public string? DiFolderId { get; init; }
}
