namespace MngDocument.Domain.Constants;

/// <summary>Teslimat omurgası kaynak tür kodları (F1-1). Katalog boşsa fallback.</summary>
public static class ResourceKindCode
{
    public const string Plan = "plan";
    public const string Minutes = "minutes";
    public const string Decision = "decision";
    public const string Deliverable = "deliverable";
    public const string Procedure = "procedure";
    public const string Specification = "specification";
    public const string Evidence = "evidence";
    public const string Diagram = "diagram";

    public static readonly IReadOnlyList<(string Code, string DisplayName, string Family, int SortOrder)> BuiltIn =
        new[]
        {
            (Plan, "Plan", "document", 10),
            (Minutes, "Tutanak", "record", 20),
            (Decision, "Karar", "record", 30),
            (Deliverable, "Teslimat", "document", 40),
            (Procedure, "Prosedur", "document", 50),
            (Specification, "Sartname", "document", 60),
            (Evidence, "Kanit", "record", 70),
            (Diagram, "Diyagram", "visual", 80)
        };
}
