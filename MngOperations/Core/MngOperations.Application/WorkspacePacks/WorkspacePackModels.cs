using System.Text.Json;
using System.Text.Json.Serialization;

namespace MngOperations.Application.WorkspacePacks;

public sealed class WorkspacePackDefinition
{
    public string Code { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SuggestedWorkspaceType { get; set; }
    public string? SuggestedPrefix { get; set; }
    public List<WorkspacePackState> States { get; set; } = [];
    public List<WorkspacePackFlow> Flows { get; set; } = [];
    public List<WorkspacePackType> Types { get; set; } = [];
    public List<WorkspacePackForm> Forms { get; set; } = [];
    public List<WorkspacePackBoard> Boards { get; set; } = [];
    public List<WorkspacePackProfile> Profiles { get; set; } = [];
    public List<string> LibraryFolders { get; set; } = [];

    [JsonIgnore]
    public bool CanApply { get; set; }
}

public sealed class WorkspacePackState
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsInitial { get; set; }
}

public sealed class WorkspacePackFlow
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public string InitialStateKey { get; set; } = string.Empty;
    public List<WorkspacePackTransition> Transitions { get; set; } = [];
}

public sealed class WorkspacePackTransition
{
    public string Key { get; set; } = string.Empty;
    public string FromStateKey { get; set; } = string.Empty;
    public string ToStateKey { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

public sealed class WorkspacePackType
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FlowKey { get; set; } = string.Empty;
    public bool RequiresParent { get; set; }
    public List<string> ChildTypeKeys { get; set; } = [];
}

public sealed class WorkspacePackForm
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public string FlowKey { get; set; } = string.Empty;
    public string TypeKey { get; set; } = string.Empty;
    public List<string> Fields { get; set; } = [];
}

public sealed class WorkspacePackBoard
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public string FlowKey { get; set; } = string.Empty;
    public string FormKey { get; set; } = string.Empty;
    public string TypeKey { get; set; } = string.Empty;
    public List<string> ColumnStateKeys { get; set; } = [];
}

public sealed class WorkspacePackProfile
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public string FlowKey { get; set; } = string.Empty;
    public string TypeKey { get; set; } = string.Empty;
    public List<string> Fields { get; set; } = [];
    public List<string> ActionKeys { get; set; } = [];
}

public sealed class WorkspacePackSummaryDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SuggestedWorkspaceType { get; set; }
    public string? SuggestedPrefix { get; set; }
    public int TypeCount { get; set; }
    public int StateCount { get; set; }
    public int FlowCount { get; set; }
    public bool CanApply { get; set; }
    public List<WorkspacePackBoardNameDto> Boards { get; set; } = [];
}

public sealed class WorkspacePackBoardNameDto
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

internal static class WorkspacePackJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}
