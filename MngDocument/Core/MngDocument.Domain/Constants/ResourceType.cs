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

/// <summary>
/// <c>dm_resources.status</c> değerleri (yalnızca markdown için anlamlı).
/// Yok/eski kayıtlar <see cref="Published"/> kabul edilir (geriye dönük uyumluluk).
/// </summary>
public static class ResourceStatus
{
    /// <summary>Taslak: kullanıcı henüz yayınlamadı.</summary>
    public const string Draft = "draft";

    /// <summary>Yayınlanmış (varsayılan).</summary>
    public const string Published = "published";

    public static string Normalize(string? value) =>
        string.Equals(value, Draft, System.StringComparison.OrdinalIgnoreCase) ? Draft : Published;
}

/// <summary>
/// Klasör yetki aksiyonları (<c>dm_resource_permissions.permissions</c> dizisi).
/// Grup bazlı; dosya/markdown içinde bulunduğu klasörün yetkisini miras alır.
/// </summary>
public static class ResourceAction
{
    public const string View = "view";
    public const string Create = "create";
    public const string Edit = "edit";
    public const string Delete = "delete";
    public const string Upload = "upload";
    public const string Download = "download";
    public const string Move = "move";
    public const string Share = "share";

    /// <summary>Bir izin matrisinde geçerli olan tüm aksiyonlar (UI/validasyon için).</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        View, Create, Edit, Delete, Upload, Download, Move, Share
    };

    public static bool IsValid(string? value) =>
        value is not null && All.Contains(value);
}
