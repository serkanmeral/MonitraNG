namespace MngDocument.Domain.Constants;

/// <summary>Yönetilen Office türü (Collabora Writer / Calc / Impress).</summary>
public enum ManagedOfficeKind
{
    Document = 0,
    Sheet = 1,
    Presentation = 2
}

/// <summary>DOCX / XLSX / PPTX için uzantı, MIME ve varsayılan ad profili.</summary>
public sealed record ManagedOfficeProfile(
    ManagedOfficeKind Kind,
    string Extension,
    string MimeType,
    string DefaultFileName);

/// <summary>Managed office profilleri — WOPI, native create ve UI tek kaynaktan okur.</summary>
public static class ManagedOfficeProfiles
{
    public static readonly ManagedOfficeProfile Document = new(
        ManagedOfficeKind.Document,
        "docx",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "document.docx");

    public static readonly ManagedOfficeProfile Sheet = new(
        ManagedOfficeKind.Sheet,
        "xlsx",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "sheet.xlsx");

    public static readonly ManagedOfficeProfile Presentation = new(
        ManagedOfficeKind.Presentation,
        "pptx",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "presentation.pptx");

    private static readonly IReadOnlyDictionary<ManagedOfficeKind, ManagedOfficeProfile> ByKind =
        new Dictionary<ManagedOfficeKind, ManagedOfficeProfile>
        {
            [ManagedOfficeKind.Document] = Document,
            [ManagedOfficeKind.Sheet] = Sheet,
            [ManagedOfficeKind.Presentation] = Presentation
        };

    public static ManagedOfficeProfile Get(ManagedOfficeKind kind) => ByKind[kind];

    public static IEnumerable<ManagedOfficeProfile> All => ByKind.Values;

    public static bool TryResolve(string? extension, string? mimeType, out ManagedOfficeProfile profile)
    {
        var ext = NormalizeExtension(extension);
        if (!string.IsNullOrEmpty(ext))
        {
            foreach (var p in ByKind.Values)
            {
                if (string.Equals(p.Extension, ext, StringComparison.OrdinalIgnoreCase))
                {
                    profile = p;
                    return true;
                }
            }
        }

        var mime = mimeType?.Trim() ?? string.Empty;
        if (mime.Contains("wordprocessingml", StringComparison.OrdinalIgnoreCase))
        {
            profile = Document;
            return true;
        }

        if (mime.Contains("spreadsheetml", StringComparison.OrdinalIgnoreCase))
        {
            profile = Sheet;
            return true;
        }

        if (mime.Contains("presentationml", StringComparison.OrdinalIgnoreCase))
        {
            profile = Presentation;
            return true;
        }

        profile = Document;
        return false;
    }

    public static bool IsManagedOfficeExtension(string? extension, string? mimeType) =>
        TryResolve(extension, mimeType, out _);

    public static string EnsureFileNameHasExtension(string? name, ManagedOfficeProfile profile)
    {
        var stem = string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(profile.DefaultFileName) : name.Trim();
        var ext = "." + profile.Extension;
        if (stem.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            return stem;
        return stem + ext;
    }

    private static string NormalizeExtension(string? extension)
    {
        var ext = (extension ?? string.Empty).Trim().TrimStart('.');
        return ext;
    }
}
