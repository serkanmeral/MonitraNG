namespace MngDocument.Application.Contracts.Templates;

public sealed class TemplateListResult
{
    public IReadOnlyList<TemplateSummaryDto> Items { get; init; } = Array.Empty<TemplateSummaryDto>();
    public long Total { get; init; }
}

public class TemplateSummaryDto
{
    public string Id { get; init; } = string.Empty;
    public string? CategoryId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Code { get; init; }
    public string? Description { get; init; }
    public string? SourceResourceId { get; init; }
    public string? SourceStoragePath { get; init; }
    public string? SourceFileName { get; init; }
    public string CreationMode { get; init; } = "fromTemplate";
    public string Status { get; init; } = "draft";
    public int ParameterCount { get; init; }
    public string? PrimaryContextType { get; init; }
    public string? GenerationProfile { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class TemplateDetailDto : TemplateSummaryDto
{
    public string SchemaVersion { get; init; } = "1.0";
    public string? PrimaryContextType { get; init; }
    public string? GenerationProfile { get; init; }
    public string? DefaultLetterheadId { get; init; }
    public string? DefaultCoverPageId { get; init; }
    public TemplatePageLayoutDto? PageLayout { get; init; }
    public TemplateLetterheadDto? Letterhead { get; init; }
    public TemplateFooterDto? Footer { get; init; }
    public IReadOnlyList<TemplateParameterDto> Parameters { get; init; } = Array.Empty<TemplateParameterDto>();
}

public sealed class TemplateParameterDto
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;

    /// <summary>scalar · table · list · chart (default scalar).</summary>
    public string Kind { get; init; } = "scalar";

    public string DataType { get; init; } = "text";
    public string ValueSourceMode { get; init; } = "manual";
    public string? DataSourceRef { get; init; }
    public string? DefaultValue { get; init; }
    public string? Format { get; init; }
    public TemplateIncrementalOptionsDto? Incremental { get; init; }
    public TemplateValueSourceModel? ValueSource { get; init; }
    public TemplateDocBindingDto? DocBinding { get; init; }

    /// <summary>Legacy API alias for <see cref="DocBinding"/>.</summary>
    public TemplateDocBindingDto? SourceBinding => DocBinding;

    public TemplateContextBindingDto? ContextBinding { get; init; }
}

public sealed class TemplateIncrementalOptionsDto
{
    public string Format { get; init; } = string.Empty;
    public int StartValue { get; init; } = 1;
    public int IncrementStep { get; init; } = 1;
    public string? ScopeKey { get; init; }
    public string ResetPolicy { get; init; } = "none";
}

public class TemplateDocBindingDto
{
    public string RegionKind { get; init; } = "paragraph";
    public int ParagraphIndex { get; init; }
    public string? OriginalText { get; init; }
    public int? CharStart { get; init; }
    public int? CharEnd { get; init; }
    public int? TableIndex { get; init; }
    public int? HeaderRowIndex { get; init; }
    public int? TemplateRowIndex { get; init; }
}

/// <summary>Legacy name — use <see cref="TemplateDocBindingDto"/>.</summary>
public sealed class TemplateSourceBindingDto : TemplateDocBindingDto;

public sealed class TemplateContextBindingDto
{
    public string Path { get; init; } = string.Empty;
    public string? FallbackPath { get; init; }
    public string? DefaultValue { get; init; }
    public string? Format { get; init; }
}

public sealed class DocxStructureDto
{
    public string TemplateId { get; init; } = string.Empty;
    public string ResourceId { get; init; } = string.Empty;
    public string? FileName { get; init; }
    public IReadOnlyList<DocxParagraphDto> Paragraphs { get; init; } = Array.Empty<DocxParagraphDto>();
    public int TableCount { get; init; }
    /// <summary>DOCX içinde bulunan <c>{{paramKey}}</c> placeholder envanteri.</summary>
    public IReadOnlyList<DocxPlaceholderDto> Placeholders { get; init; } = Array.Empty<DocxPlaceholderDto>();
    public IReadOnlyList<string> PlaceholderWarnings { get; init; } = Array.Empty<string>();
}

public sealed class DocxPlaceholderDto
{
    public string Key { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
    public int OccurrenceCount { get; init; }
}

public sealed class DocxParagraphDto
{
    public int Index { get; init; }
    public string Text { get; init; } = string.Empty;
}
