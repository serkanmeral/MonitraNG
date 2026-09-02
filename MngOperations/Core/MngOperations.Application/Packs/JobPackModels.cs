using System.Text.Json;

namespace MngOperations.Application.Packs;

public sealed class JobPackDefinition
{
    public string Code { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> Kinds { get; set; } = [];
    public List<JobPackFolder> Folders { get; set; } = [];
    public List<JobPackWbsNode> Wbs { get; set; } = [];
    public List<JobPackStarter> Starters { get; set; } = [];
    public JobPackDiagram? Diagram { get; set; }
}

public sealed class JobPackFolder
{
    public string Name { get; set; } = string.Empty;
}

public sealed class JobPackWbsNode
{
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = "task";
    public double? Weight { get; set; }
    public List<JobPackWbsNode> Children { get; set; } = [];
}

public sealed class JobPackStarter
{
    public string Folder { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Kind { get; set; }
    public string? Body { get; set; }
}

public sealed class JobPackDiagram
{
    public string Folder { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Kind { get; set; }
}

internal static class JobPackJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}
