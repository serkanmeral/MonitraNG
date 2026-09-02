namespace MngDocument.Domain.Constants;

/// <summary><c>dm_resource_links</c> hedef modül ve tip sabitleri.</summary>
public static class ResourceLinkTargetModule
{
    public const string OperationCore = "operationCore";
    public const string DocumentIntelligence = "documentIntelligence";
}

public static class ResourceLinkTargetType
{
    public const string WorkItem = "workItem";
    public const string Resource = "resource";
}

public static class ResourceLinkRelationType
{
    public const string Reference = "reference";
    public const string Attachment = "attachment";
    public const string Evidence = "evidence";
    public const string Output = "output";
    public const string DerivedFrom = "derivedFrom";
    public const string Implements = "implements";
    public const string DependsOn = "dependsOn";
    public const string Supersedes = "supersedes";
    public const string ConflictsWith = "conflictsWith";

    public static readonly IReadOnlyList<string> BuiltIn = new[]
    {
        Reference,
        Attachment,
        Evidence,
        Output,
        DerivedFrom,
        Implements,
        DependsOn,
        Supersedes,
        ConflictsWith
    };

    private static readonly HashSet<string> BuiltInSet = new(BuiltIn, StringComparer.OrdinalIgnoreCase);

    public static bool IsBuiltIn(string? value) =>
        !string.IsNullOrWhiteSpace(value) && BuiltInSet.Contains(value.Trim());

    public static bool IsWorkItemTarget(string? module, string? type) =>
        string.Equals(module, ResourceLinkTargetModule.OperationCore, StringComparison.OrdinalIgnoreCase)
        && string.Equals(type, ResourceLinkTargetType.WorkItem, StringComparison.OrdinalIgnoreCase);

    public static bool IsResourceTarget(string? module, string? type) =>
        string.Equals(module, ResourceLinkTargetModule.DocumentIntelligence, StringComparison.OrdinalIgnoreCase)
        && string.Equals(type, ResourceLinkTargetType.Resource, StringComparison.OrdinalIgnoreCase);
}

/// <summary>OperationCore work item dataset adı (DG).</summary>
public static class OcDatasets
{
    public const string WorkItems = "op_work_items";
}
