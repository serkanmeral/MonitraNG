namespace MngDocument.Application.Contracts.Generation;

public sealed class GenerateDocumentRequest
{
    public string? ProfileCode { get; set; }
    public string? TemplateCode { get; set; }
    public DocumentGenerationContextDto Context { get; set; } = new();
    public Dictionary<string, string>? Overrides { get; set; }
    public DocumentGenerationRuntimeDto? Runtime { get; set; }
    /// <summary>D-BR2: kapak ekle. false = zorla kapak yok; true = ekle; null = şablonda defaultCoverPageId varsa ekle.</summary>
    public bool? IncludeCoverPage { get; set; }
    /// <summary>D-BR2: kapak katalog kaydı; boşsa şablon varsayılanı veya katalog default.</summary>
    public string? CoverPageId { get; set; }
}

public sealed class DocumentGenerationContextDto
{
    public string Type { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
}

public sealed class GenerateDocumentResultDto
{
    public string ProfileCode { get; init; } = string.Empty;
    public string ContextType { get; init; } = string.Empty;
    public string ContextId { get; init; } = string.Empty;
    public string TemplateId { get; init; } = string.Empty;
    public string TemplateCode { get; init; } = string.Empty;
    public string? LetterheadId { get; init; }
    public string? LetterheadCode { get; init; }
    public string? LetterheadName { get; init; }
    public string? CoverPageId { get; init; }
    public string? CoverPageCode { get; init; }
    public string? CoverPageName { get; init; }
    public string? DocNo { get; init; }
    public string ResourceId { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public IReadOnlyList<string> FolderPath { get; init; } = Array.Empty<string>();
    public DateTime GeneratedAt { get; init; }
    public IReadOnlyDictionary<string, string> ResolvedValues { get; init; }
        = new Dictionary<string, string>();
    /// <summary>Placeholder keys found in DOCX but missing from the template parameter model.</summary>
    public IReadOnlyList<string> UndefinedParameterKeys { get; init; } = Array.Empty<string>();
    /// <summary>Defined parameters present in DOCX with empty/missing resolved values.</summary>
    public IReadOnlyList<string> UnresolvedParameterKeys { get; init; } = Array.Empty<string>();
    /// <summary>Placeholder keys still present in the generated DOCX after merge.</summary>
    public IReadOnlyList<string> RemainingPlaceholderKeys { get; init; } = Array.Empty<string>();
    public bool HasParameterWarnings =>
        UndefinedParameterKeys.Count > 0 || UnresolvedParameterKeys.Count > 0;
}

public sealed class DocumentGenerationStatusDto
{
    public string ProfileCode { get; init; } = string.Empty;
    public string ContextType { get; init; } = string.Empty;
    public string ContextId { get; init; } = string.Empty;
    public bool Generated { get; init; }
    public string? DocNo { get; init; }
    public string? ResourceId { get; init; }
    public string? FileName { get; init; }
    public DateTime? GeneratedAt { get; init; }
}

public sealed class DocumentContextTypeDto
{
    public string Type { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string RootDataset { get; init; } = string.Empty;
    public IReadOnlyList<DocumentContextFieldDto> Fields { get; init; } = Array.Empty<DocumentContextFieldDto>();
}

public sealed class DocumentContextFieldDto
{
    public string Path { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string DataType { get; init; } = "text";
}

public sealed class DocumentProducerDto
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string ContextType { get; init; } = string.Empty;
    public string TemplateCode { get; init; } = string.Empty;
}

public sealed class DocumentProducerDetailDto
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string ContextType { get; init; } = string.Empty;
    public string TemplateCode { get; init; } = string.Empty;
    public string OutputFormat { get; init; } = "docx";
    public IReadOnlyList<string> OutputFolderPath { get; init; } = Array.Empty<string>();
    public string FileNamePattern { get; init; } = string.Empty;
    public string? IdempotencyDataset { get; init; }
    public string? IdempotencyGuardField { get; init; }
    public IReadOnlyList<string> WritebackFields { get; init; } = Array.Empty<string>();
}

public sealed class DocumentDataSourceSummaryDto
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string Mode { get; init; } = string.Empty;
    public string? Dataset { get; init; }
    public string? Query { get; init; }
    public IReadOnlyDictionary<string, object?>? Match { get; init; }
    public int ColumnCount { get; init; }
}

public sealed class DocumentDataSourceDetailDto
{
    public string Code { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string Mode { get; init; } = string.Empty;
    public string? Dataset { get; init; }
    public string? Query { get; init; }
    public IReadOnlyDictionary<string, object?>? Match { get; init; }
    public int ColumnCount { get; init; }
    public string? QueryName { get; init; }
    public string? IdFrom { get; init; }
    public IReadOnlyDictionary<string, object?>? Parameters { get; init; }
    public IReadOnlyList<DocumentDataSourceColumnDto> Columns { get; init; } = Array.Empty<DocumentDataSourceColumnDto>();
}

public sealed class DocumentDataSourceColumnDto
{
    public string SourceField { get; init; } = string.Empty;
    public string? Header { get; init; }
    public string? Format { get; init; }
}

public sealed class DocumentGenerationPreviewDto
{
    public string ProfileCode { get; init; } = string.Empty;
    public string ContextType { get; init; } = string.Empty;
    public string ContextId { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string> Values { get; init; }
        = new Dictionary<string, string>();
    public IReadOnlyList<string> MissingKeys { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> UndefinedParameterKeys { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> UnresolvedParameterKeys { get; init; } = Array.Empty<string>();
}

/// <summary>DI ağacında şablondan manuel döküman üretimi (D4).</summary>
public sealed class GenerateFromTemplateRequest
{
    /// <summary>Hedef klasör (<c>dm_resources</c> folder id).</summary>
    public string ParentFolderId { get; set; } = string.Empty;

    /// <summary>Kaynak ağacında görünen ad; boşsa dosya adından türetilir.</summary>
    public string? DocumentName { get; set; }

    /// <summary>Parametre anahtar → değer (manual/context override).</summary>
    public Dictionary<string, string>? Overrides { get; set; }

    /// <summary>
    /// Tablo parametreleri (kind=table) — reporting filtrelenmiş satırlar vb.
    /// Key = parametre key; value = satır sözlükleri.
    /// </summary>
    public Dictionary<string, List<Dictionary<string, object?>>>? TableOverrides { get; set; }

    /// <summary>
    /// true (varsayılan): boş/eksik skaler placeholder'lar belgede kalır.
    /// false: boş string ile değiştirilir.
    /// </summary>
    public bool PreserveMissingPlaceholders { get; set; } = true;

    /// <summary>true ise kapak sayfası birleştirilir (yalnızca DOCX).</summary>
    public bool? IncludeCoverPage { get; set; }

    /// <summary>Opsiyonel kapak katalog id; boşsa şablon varsayılanı veya katalog varsayılanı.</summary>
    public string? CoverPageId { get; set; }
}

/// <summary>Şablondan üretim önizlemesi — parametre çözümlemesi (D4).</summary>
public sealed class PreviewFromTemplateRequest
{
    public Dictionary<string, string>? Overrides { get; set; }

    /// <summary>true ise incremental sayaçlar ayrılır (varsayılan false).</summary>
    public bool AllocateCounters { get; set; }

    /// <summary>Önizlemede antet başlığı vb. için döküman adı.</summary>
    public string? DocumentName { get; set; }
}

/// <summary>Şablondan üretim Collabora önizleme oturumu (merge + antet, salt okunur).</summary>
public sealed class TemplateGenerationPreviewSessionDto
{
    public string TemplateId { get; init; } = string.Empty;
    public string EditorUrl { get; init; } = string.Empty;
    public string AccessToken { get; init; } = string.Empty;
    public string WopiSrc { get; init; } = string.Empty;
    public bool ReadOnly { get; init; } = true;
    public string ProfileCode { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string> Values { get; init; }
        = new Dictionary<string, string>();
    public IReadOnlyList<string> MissingKeys { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> UndefinedParameterKeys { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> UnresolvedParameterKeys { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> RemainingPlaceholderKeys { get; init; } = Array.Empty<string>();
}
