using MngDocument.Application.Contracts.Letterheads;

namespace MngDocument.Application.Contracts.Resources;

public sealed class CreateFolderRequest
{
    public string? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string>? Tags { get; set; }
}

public sealed class RenameResourceRequest
{
    public string Name { get; set; } = string.Empty;
}

public sealed class MoveResourceRequest
{
    /// <summary>Hedef klasör id'si. <c>null</c> ise kök seviyeye taşınır.</summary>
    public string? NewParentId { get; set; }
}

public sealed class CreateMarkdownRequest
{
    public string? ParentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string>? Tags { get; set; }

    /// <summary><c>true</c> ise doküman taslak olarak oluşturulur (<c>status=draft</c>); aksi halde yayınlanmış.</summary>
    public bool IsDraft { get; set; }
}

public sealed class UpdateMarkdownRequest
{
    public string? Title { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string>? Tags { get; set; }

    /// <summary>
    /// Optimistic concurrency: istemcinin elindeki sürüm. Sunucudaki güncel sürümden
    /// farklıysa 409 döner (başka biri kaydetmiş demektir).
    /// </summary>
    public int ExpectedVersionNumber { get; set; }

    /// <summary>
    /// Doküman durumu: <c>true</c> = taslak, <c>false</c> = yayınla. <c>null</c> ise mevcut durum korunur.
    /// </summary>
    public bool? IsDraft { get; set; }

    /// <summary>
    /// Sürüm kaydına yazılacak değişiklik notu. Boşsa varsayılan <c>update</c> kullanılır.
    /// </summary>
    public string? ChangeNote { get; set; }
}

/// <summary>
/// Dosya kaynağı oluşturur. Binary içerik base64 olarak <see cref="Content"/> ile gelir ve
/// DG <c>dm_resources.file</c> (fieldType=file) alanına işlenir; DG MinIO'ya yükleyip path'i kaydeder.
/// </summary>
public sealed class CreateFileResourceRequest
{
    public string? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? MimeType { get; set; }
    public string? Extension { get; set; }
    public long? Size { get; set; }
    public List<string>? Tags { get; set; }

    /// <summary>Base64 dosya içeriği (data URL öneki olmadan). DG <c>file</c> alanına işlenir.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Orijinal dosya adı (MinIO metadata'sı; indirmede dosya adı olarak kullanılır).</summary>
    public string? OriginalFileName { get; set; }

    public string? Origin { get; set; }
    public string? TemplateId { get; set; }
    public string? TemplateCode { get; set; }
    public string? GenerationProfile { get; set; }
    public string? LetterheadId { get; set; }
    public string? CoverPageId { get; set; }
    public string? DocumentNo { get; set; }
}

/// <summary>
/// Yeni native DOCX döküman oluşturur (<c>origin=native</c>). Antet opsiyonel;
/// seçilen antet header parametreleri oluşturma anında doldurulur.
/// </summary>
public sealed class CreateNativeDocumentRequest
{
    public string? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// İş kodu (<c>dm_resources.documentNo</c>). Domain geneli benzersiz; antet docNo'dan bağımsızdır.
    /// </summary>
    public string DocumentNo { get; set; } = string.Empty;

    public string? Description { get; set; }
    public List<string>? Tags { get; set; }

    /// <summary>Opsiyonel antet katalog id. Boşsa antetsiz boş DOCX oluşturulur.</summary>
    public string? LetterheadId { get; set; }

    /// <summary>
    /// Antet seçildiyse hangi header parametrelerinin doldurulacağı.
    /// Boşsa antet tanımındaki <see cref="Letterheads.LetterheadHeaderFieldsDto"/> varsayılanları kullanılır.
    /// </summary>
    public LetterheadHeaderFieldsDto? SelectedHeaderFields { get; set; }
}

/// <summary>
/// Boş XLSX / PPTX (managed office) oluşturma — antet uygulanmaz.
/// </summary>
public sealed class CreateNativeOfficeRequest
{
    public string? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// İş kodu (<c>dm_resources.documentNo</c>). Boş bırakılırsa dosya adından üretilir.
    /// </summary>
    public string? DocumentNo { get; set; }

    public string? Description { get; set; }
    public List<string>? Tags { get; set; }
}

/// <summary>
/// Bir klasörün grup bazlı yetki matrisini değiştirir. Yalnızca yetki mirası kırık (anchor)
/// klasörlerde uygulanır. Listede yer almayan gruplar kaldırılır; boş <see cref="GroupPermissionInput.Permissions"/>
/// olan gruplar da kaldırılır.
/// </summary>
public sealed class SetFolderPermissionsRequest
{
    public List<GroupPermissionInput> Groups { get; set; } = new();
}

public sealed class GroupPermissionInput
{
    public string? GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public List<string> Permissions { get; set; } = new();
}

/// <summary>Kaynak metadata güncellemesi (etiketler, açıklama).</summary>
public sealed class UpdateResourceMetadataRequest
{
    public List<string>? Tags { get; set; }
    public string? Description { get; set; }
}

/// <summary>Yönetilen DOCX sürüm kaydının değişiklik notunu günceller.</summary>
public sealed class UpdateFileVersionChangeNoteRequest
{
    /// <summary>Sürüm geçmişinde görünecek not. Boşsa varsayılan <c>save</c> kullanılır.</summary>
    public string? ChangeNote { get; set; }
}

/// <summary>Markdown sayfa veya manual DOCX klonlama isteği.</summary>
public sealed class CloneResourceRequest
{
    /// <summary>Hedef klasör. <c>null</c> ise kök.</summary>
    public string? ParentId { get; set; }

    /// <summary>Yeni ad / başlık.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Manual DOCX için yeni iş kodu (<c>documentNo</c>). Markdown için yok sayılır.</summary>
    public string? DocumentNo { get; set; }
}
