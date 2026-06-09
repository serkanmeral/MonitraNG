namespace MngOperations.Application.Models;

public sealed class MetadataCacheReloadResult
{
    public required string WorkspaceId { get; init; }
    public int KeysRemoved { get; init; }
}
