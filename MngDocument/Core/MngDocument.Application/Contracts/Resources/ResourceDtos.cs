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
    public string? ClassificationTagId { get; init; }
    public string? Kind { get; init; }
    public string? ContentType { get; init; }
    public string? MimeType { get; init; }
    public string? Extension { get; init; }
    public long? Size { get; init; }
    public int CurrentVersionNumber { get; init; }
    public bool HasContent { get; init; }

    /// <summary>Doküman durumu (<c>draft</c>/<c>inReview</c>/<c>published</c>). Varsayılan <c>published</c>.</summary>
    public string Status { get; init; } = "published";

    public string? SubmittedBy { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public string? ApprovedBy { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public string? ReviewNote { get; init; }

    public int? BaselineVersionNumber { get; init; }
    public string? BaselineSetBy { get; init; }
    public DateTime? BaselineSetAt { get; init; }
    public string? BaselineNote { get; init; }

    /// <summary>Baseline varsa ve güncel sürüm farklıysa sapma.</summary>
    public bool BaselineDrifted { get; init; }

    /// <summary>Yüklenen dosyanın MinIO path'i (yalnızca <c>type=file</c>). İndirme için kullanılır.</summary>
    public string? FilePath { get; init; }

    /// <summary>Yüklenen dosyanın orijinal adı (yalnızca <c>type=file</c>).</summary>
    public string? FileName { get; init; }
    public string? Origin { get; init; }
    public string? TemplateId { get; init; }
    public string? TemplateCode { get; init; }
    public string? GenerationProfile { get; init; }
    public string? LetterheadId { get; init; }
    public string? DocumentNo { get; init; }

    /// <summary>none, running, done, failed veya okumada hesaplanan stale.</summary>
    public string IndexStatus { get; init; } = "none";
    public DateTime? IndexedAt { get; init; }
    public string? IndexError { get; init; }
    public int? IndexCharCount { get; init; }

    /// <summary>none, running, done, failed veya okumada hesaplanan stale.</summary>
    public string SummaryStatus { get; init; } = "none";
    public string? SummaryText { get; init; }
    public DateTime? SummarizedAt { get; init; }
    public string? SummaryError { get; init; }
    public string? SummaryStep { get; init; }
    public bool SummaryChunked { get; init; }
    public int SummaryCompleted { get; init; }
    public int SummaryTotal { get; init; }
    public DateTime? SummaryUpdatedAt { get; init; }
    public IReadOnlyList<string> SummaryLog { get; init; } = Array.Empty<string>();

    public DateTime? CreatedAt { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }

    /// <summary>Geçerli kullanıcının bu kaynak üzerindeki etkin yetkileri (UI buton gating için).</summary>
    public EffectivePermissionDto Permissions { get; init; } = EffectivePermissionDto.Full;
}

/// <summary>
/// Geçerli kullanıcının bir kaynak üzerindeki çözülmüş (miras dahil) etkin yetkileri.
/// Admin için tüm alanlar <c>true</c>; açık varsayılan (hiç ACL yok) durumunda da tümü <c>true</c>.
/// </summary>
public sealed record EffectivePermissionDto
{
    public bool CanView { get; init; }
    public bool CanCreate { get; init; }
    public bool CanEdit { get; init; }
    public bool CanDelete { get; init; }
    public bool CanUpload { get; init; }
    public bool CanDownload { get; init; }
    public bool CanMove { get; init; }
    public bool CanShare { get; init; }

    /// <summary>Tüm aksiyonlar açık (admin / açık varsayılan).</summary>
    public static readonly EffectivePermissionDto Full = new()
    {
        CanView = true,
        CanCreate = true,
        CanEdit = true,
        CanDelete = true,
        CanUpload = true,
        CanDownload = true,
        CanMove = true,
        CanShare = true
    };
}

/// <summary>Bir grup için verilen yetki aksiyonları.</summary>
public sealed record GroupPermissionDto
{
    public string? GroupId { get; init; }
    public string GroupName { get; init; } = string.Empty;
    public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Bir klasörün yetki yönetim görünümü: miras durumu + kendi grup matrisi
/// (miras alıyorsa en yakın anchor'dan miras alınan etkin matris).
/// </summary>
public sealed record FolderPermissionsDto
{
    public string ResourceId { get; init; } = string.Empty;

    /// <summary>Bu klasörün kendi ACL'i var mı (miras kırık mı).</summary>
    public bool InheritanceBroken { get; init; }

    /// <summary>Etkin yetkilerin geldiği anchor klasör id'si (miras kaynağı). Kendisi anchor ise kendi id'si.</summary>
    public string? EffectiveAnchorId { get; init; }

    /// <summary>Grup → verilen aksiyonlar matrisi (anchor değilse miras alınan etkin matris gösterilir).</summary>
    public IReadOnlyList<GroupPermissionDto> Groups { get; init; } = Array.Empty<GroupPermissionDto>();

    /// <summary>Geçerli kullanıcının bu klasör üzerindeki etkin yetkileri.</summary>
    public EffectivePermissionDto Effective { get; init; } = EffectivePermissionDto.Full;
}

/// <summary>Sol panel ağaç düğümü (yalnızca klasörler).</summary>
public sealed record TreeNodeDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ParentId { get; init; }
    /// <summary>Alt klasör var mı (lazy tree; <see cref="Children"/> yüklenmemiş olabilir).</summary>
    public bool HasChildren { get; set; }
    public List<TreeNodeDto> Children { get; init; } = new();
}

/// <summary>Derin link: breadcrumb yolu boyunca her seviyenin kardeş klasör listesi.</summary>
public sealed record TreePathSegmentDto
{
    public string? ParentId { get; init; }
    public IReadOnlyList<TreeNodeDto> Nodes { get; init; } = Array.Empty<TreeNodeDto>();
}

public sealed record TreePathDto
{
    public IReadOnlyList<BreadcrumbDto> Breadcrumb { get; init; } = Array.Empty<BreadcrumbDto>();
    public IReadOnlyList<TreePathSegmentDto> Segments { get; init; } = Array.Empty<TreePathSegmentDto>();
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

/// <summary>
/// Ana ekran ilk yükleme / yenileme: ağaç + geçerli klasör içeriği (tek snapshot).
/// <paramref name="FolderId"/> verilirse breadcrumb + seçili klasör metadata'sı da döner.
/// </summary>
public sealed record ResourceBootstrapDto
{
    /// <summary>Lazy tree kök seviyesi (yalnızca birinci seviye klasörler).</summary>
    public IReadOnlyList<TreeNodeDto> TreeRoots { get; init; } = Array.Empty<TreeNodeDto>();

    /// <summary>Eski tam ağaç (geriye dönük; yeni UI <see cref="TreeRoots"/> kullanır).</summary>
    public IReadOnlyList<TreeNodeDto> Tree { get; init; } = Array.Empty<TreeNodeDto>();
    public ResourceListResult Children { get; init; } = new();
    public IReadOnlyList<BreadcrumbDto> Breadcrumb { get; init; } = Array.Empty<BreadcrumbDto>();
    public ResourceDto? SelectedFolder { get; init; }
}

/// <summary>Klasör gezinme: içerik listesi + breadcrumb + seçili klasör (ağaç hariç, tek snapshot).</summary>
public sealed record ResourceBrowseContextDto
{
    public ResourceListResult Children { get; init; } = new();
    public IReadOnlyList<BreadcrumbDto> Breadcrumb { get; init; } = Array.Empty<BreadcrumbDto>();
    public ResourceDto? SelectedFolder { get; init; }
}

/// <summary>Belge özetinin durumu. Üretim bitene kadar adım alanı dolar.</summary>
public sealed record ResourceSummaryResultDto
{
    public string Status { get; init; } = "none";
    public string? Text { get; init; }
    public string? Error { get; init; }
    public string? Step { get; init; }
    public bool Chunked { get; init; }
    public DateTime? SummarizedAt { get; init; }
    public int Completed { get; init; }
    public int Total { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public IReadOnlyList<string> Log { get; init; } = Array.Empty<string>();
}

public sealed class ResourceDiffRequest
{
    public List<string> OtherIds { get; set; } = new();
}

public sealed record ResourceDiffResultDto
{
    public string BaseId { get; init; } = string.Empty;
    public string BaseName { get; init; } = string.Empty;
    public IReadOnlyList<ResourceDiffComparisonDto> Comparisons { get; init; } = Array.Empty<ResourceDiffComparisonDto>();
}

public sealed record ResourceDiffComparisonDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public bool Compared { get; init; }
    public string? Reason { get; init; }
    public int UnchangedCount { get; init; }
    public bool Truncated { get; init; }
    public IReadOnlyList<ResourceDiffLineDto> Added { get; init; } = Array.Empty<ResourceDiffLineDto>();
    public IReadOnlyList<ResourceDiffLineDto> Removed { get; init; } = Array.Empty<ResourceDiffLineDto>();
    public IReadOnlyList<ResourceDiffChangeDto> Changed { get; init; } = Array.Empty<ResourceDiffChangeDto>();
    public int AddedCount { get; init; }
    public int RemovedCount { get; init; }
    public int ChangedCount { get; init; }
    public IReadOnlyList<ResourceDiffLineDto> NoiseAdded { get; init; } = Array.Empty<ResourceDiffLineDto>();
    public IReadOnlyList<ResourceDiffLineDto> NoiseRemoved { get; init; } = Array.Empty<ResourceDiffLineDto>();
    public IReadOnlyList<ResourceDiffChangeDto> NoiseChanged { get; init; } = Array.Empty<ResourceDiffChangeDto>();
    public int NoiseAddedCount { get; init; }
    public int NoiseRemovedCount { get; init; }
    public int NoiseChangedCount { get; init; }
}

public sealed record ResourceDiffLineDto
{
    public string Text { get; init; } = string.Empty;
    public string Heading { get; init; } = string.Empty;
}

public sealed record ResourceDiffChangeDto
{
    public string Base { get; init; } = string.Empty;
    public string Other { get; init; } = string.Empty;
    public string Heading { get; init; } = string.Empty;
}

public sealed record ResourceIndexResultDto
{
    public string Status { get; init; } = "none";
    public int CharCount { get; init; }
    public string? Error { get; init; }
    public DateTime? IndexedAt { get; init; }
    public IReadOnlyList<string> Steps { get; init; } = Array.Empty<string>();
    public ResourceDto Resource { get; init; } = new();
}

/// <summary>Klasör ağacındaki toplu indeks işi. Kapatmak işi iptal etmez.</summary>
public sealed record BulkIndexJobDto
{
    public string Status { get; init; } = "none";
    public string? FolderId { get; init; }
    public string? FolderName { get; init; }
    public string? CurrentName { get; init; }
    public int Total { get; init; }
    public int IndexedCount { get; init; }
    public int SkippedCount { get; init; }
    public int FailedCount { get; init; }
    public IReadOnlyList<BulkIndexItemDto> IndexedItems { get; init; } = Array.Empty<BulkIndexItemDto>();
    public IReadOnlyList<BulkIndexItemDto> SkippedItems { get; init; } = Array.Empty<BulkIndexItemDto>();
    public IReadOnlyList<BulkIndexItemDto> FailedItems { get; init; } = Array.Empty<BulkIndexItemDto>();
}

public sealed record BulkIndexItemDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Error { get; init; }
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
