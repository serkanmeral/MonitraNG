namespace MngDocument.Domain.Constants;

/// <summary>
/// <c>dm_resources.type</c> değerleri. Yalnızca <see cref="Folder"/> altına başka kaynak konabilir.
/// </summary>
public static class ResourceType
{
    public const string Folder = "folder";
    public const string Markdown = "markdown";
    public const string File = "file";

    public static bool IsValid(string? value) =>
        value is Folder or Markdown or File;
}

/// <summary>
/// <c>dm_resources.contentType</c> değerleri.
/// </summary>
public static class ResourceContentType
{
    public const string Markdown = "markdown";
    public const string Binary = "binary";
}
