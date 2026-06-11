namespace MngDocument.Domain.Constants;

/// <summary><c>dm_resource_links</c> hedef modül ve tip sabitleri (Faz 2).</summary>
public static class ResourceLinkTargetModule
{
    public const string OperationCore = "operationCore";
}

public static class ResourceLinkTargetType
{
    public const string WorkItem = "workItem";
}

public static class ResourceLinkRelationType
{
    public const string Reference = "reference";
    public const string Attachment = "attachment";
    public const string Evidence = "evidence";
    public const string Output = "output";

    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        Reference,
        Attachment,
        Evidence,
        Output
    };

    public static bool IsAllowed(string? value) =>
        !string.IsNullOrWhiteSpace(value) && Allowed.Contains(value.Trim());
}

/// <summary>OperationCore work item dataset adı (DG).</summary>
public static class OcDatasets
{
    public const string WorkItems = "op_work_items";
}
