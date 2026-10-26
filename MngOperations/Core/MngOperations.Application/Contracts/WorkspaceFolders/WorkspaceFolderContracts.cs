namespace MngOperations.Application.Contracts.WorkspaceFolders;

public sealed class WorkspaceFolderDto
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string? ParentFolderId { get; init; }
    public IReadOnlyList<string> ViewGroups { get; init; } = Array.Empty<string>();
    public int SortOrder { get; init; }
}

public sealed class CreateWorkspaceFolderRequest
{
    public string? Name { get; set; }
    public string? ParentFolderId { get; set; }
    public List<string>? ViewGroups { get; set; }
}

public sealed class UpdateWorkspaceFolderRequest
{
    public string? Name { get; set; }
    public List<string>? ViewGroups { get; set; }
}

public sealed class MoveWorkspaceFolderRequest
{
    public string? ParentFolderId { get; set; }
}

public sealed class MoveWorkspaceToFolderRequest
{
    public string? FolderId { get; set; }
}
