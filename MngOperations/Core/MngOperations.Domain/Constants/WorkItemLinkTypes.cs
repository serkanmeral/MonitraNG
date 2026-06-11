namespace MngOperations.Domain.Constants;

public static class WorkItemLinkTypes
{
    public const string RelatesTo = "relates_to";
    public const string Blocks = "blocks";
    public const string Duplicates = "duplicates";

    public static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        RelatesTo,
        Blocks,
        Duplicates
    };

    public static bool IsAllowed(string? linkType) =>
        !string.IsNullOrWhiteSpace(linkType) && Allowed.Contains(linkType.Trim());
}
