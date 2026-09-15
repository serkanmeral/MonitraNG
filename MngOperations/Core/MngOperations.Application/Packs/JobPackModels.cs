using System.Text.Json;
using System.Text.Json.Serialization;

namespace MngOperations.Application.Packs;

public sealed class JobPackDefinition
{
    public string Code { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Origin { get; set; }
    public string? Publisher { get; set; }
    public List<string> Kinds { get; set; } = [];
    public List<JobPackFolder> Folders { get; set; } = [];
    public List<JobPackWbsNode> Wbs { get; set; } = [];
    public List<JobPackStarter> Starters { get; set; } = [];
    public JobPackDiagram? Diagram { get; set; }
    public JobPackWorkspace? Workspace { get; set; }

    [JsonIgnore]
    public string ContentSha256 { get; set; } = string.Empty;

    [JsonIgnore]
    public bool Verified { get; set; }

    [JsonIgnore]
    public bool CanApply { get; set; }
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

public sealed class JobPackWorkspace
{
    public List<JobPackRule> Rules { get; set; } = [];
    public List<JobPackSlaPolicy> SlaPolicies { get; set; } = [];
    public List<JobPackDashboard> Dashboards { get; set; } = [];
}

public sealed class JobPackRule
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string RuleType { get; set; } = "validation";
    public string Trigger { get; set; } = "WorkItemTransition";
    public string? TransitionKey { get; set; }
    public string? ApplyMode { get; set; }
    public JsonElement? Conditions { get; set; }
    public string? ErrorMessage { get; set; }
    public int? Priority { get; set; }
}

public sealed class JobPackSlaPolicy
{
    public string Name { get; set; } = string.Empty;
    public int? ResponseTargetMinutes { get; set; }
    public int? ResolveTargetMinutes { get; set; }
    public int? Priority { get; set; }
}

public sealed class JobPackDashboard
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Scope { get; set; } = "workspace";
    public bool? IsDefault { get; set; }
    public JsonElement? Layout { get; set; }
    public JsonElement? Widgets { get; set; }
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
