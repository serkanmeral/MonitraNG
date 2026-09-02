namespace MngDocument.Domain.Constants;

public static class TagKinds
{
    public const string Organizational = "organizational";
    public const string Classification = "classification";

    public static string Normalize(string? kind)
    {
        if (string.Equals(kind, Classification, StringComparison.OrdinalIgnoreCase))
            return Classification;
        return Organizational;
    }

    public static bool IsClassification(string? kind) =>
        string.Equals(Normalize(kind), Classification, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Office OPC custom.xml property names; PDF uses a <c>% MngDlp:</c> comment before %%EOF.</summary>
public static class ClassificationStampKeys
{
    public const int SchemaVersion = 1;
    public const string Id = "MngDlp.ClassificationId";
    public const string Name = "MngDlp.ClassificationName";
    public const string Sensitivity = "MngDlp.Sensitivity";
    public const string Version = "MngDlp.SchemaVersion";
    public const string PdfKeywordsPrefix = "MngDlp:";
}
