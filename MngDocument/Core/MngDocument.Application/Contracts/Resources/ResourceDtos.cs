namespace MngDocument.Application.Contracts.Resources;

/// <summary>Tek kaynak (klasör / markdown / dosya) çıktısı.</summary>
public sealed record ResourceDto
{
    public string Id { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string? ParentId { get; init; }
    public IReadOnlyList<string> AncestorIds { get; init; } = Array.Empty<string>();
    public string Name { get; init; } = string.Empty;
    public string? Title { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public string? ContentType { get; init; }
    public string? MimeType { get; init; }
    public string? Extension { get; init; }
    public long? Size { get; init; }
    public int CurrentVersionNumber { get; init; }
    public bool HasContent { get; init; }

    /// <summary>Yüklenen dosyanın MinIO path'i (yalnızca <c>type=file</c>). İndirme için kullanılır.</summary>
    public string? FilePath { get; init; }

    /// <summary>Yüklenen dosyanın orijinal adı (yalnızca <c>type=file</c>).</summary>
    public string? FileName { get; init; }
    public DateTime? CreatedAt { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }
}

/// <summary>Sol panel ağaç düğümü (yalnızca klasörler).</summary>
public sealed record TreeNodeDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ParentId { get; init; }
    public List<TreeNodeDto> Children { get; init; } = new();
}

/// <summary>Breadcrumb / yol bilgisi.</summary>
public sealed record BreadcrumbDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

public sealed record ResourceListResult
{
    public IReadOnlyList<ResourceDto> Items { get; init; } = Array.Empty<ResourceDto>();
    public long Total { get; init; }
}

public sealed record MarkdownContentDto
{
    public string Id { get; init; } = string.Empty;
    public string? Title { get; init; }
    public string Content { get; init; } = string.Empty;
    public int CurrentVersionNumber { get; init; }
}

/// <summary>Markdown sürüm geçmişi satırı (içerik hariç).</summary>
public sealed record MarkdownVersionDto
{
    public int VersionNumber { get; init; }
    public string? ChangeNote { get; init; }
    public long? Size { get; init; }
    public DateTime? CreatedAt { get; init; }
    public string? CreatedBy { get; init; }
    public bool IsCurrent { get; init; }
}

/// <summary>Tek bir markdown sürümünün içeriği.</summary>
public sealed record MarkdownVersionContentDto
{
    public int VersionNumber { get; init; }
    public string Content { get; init; } = string.Empty;
    public string? ChangeNote { get; init; }
    public DateTime? CreatedAt { get; init; }
    public string? CreatedBy { get; init; }
}
