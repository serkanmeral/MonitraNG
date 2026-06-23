namespace MngDocument.Application.Contracts.Templates;

public sealed class TemplateListResult
{
    public IReadOnlyList<TemplateSummaryDto> Items { get; init; } = Array.Empty<TemplateSummaryDto>();
    public long Total { get; init; }
}

public class TemplateSummaryDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string SourceResourceId { get; init; } = string.Empty;
    public string? SourceFileName { get; init; }
    public string CreationMode { get; init; } = "fromTemplate";
    public string Status { get; init; } = "draft";
    public int ParameterCount { get; init; }
    public string? CreatedBy { get; init; }
    public DateTime? CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class TemplateDetailDto : TemplateSummaryDto
{
    public string SchemaVersion { get; init; } = "1.0";
    public IReadOnlyList<TemplateParameterDto> Parameters { get; init; } = Array.Empty<TemplateParameterDto>();
}

public sealed class TemplateParameterDto
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string DataType { get; init; } = "text";
    public string ValueSourceMode { get; init; } = "manual";
    public TemplateIncrementalOptionsDto? Incremental { get; init; }
    public TemplateSourceBindingDto? SourceBinding { get; init; }
}

public sealed class TemplateIncrementalOptionsDto
{
    public string Format { get; init; } = string.Empty;
    public int StartValue { get; init; } = 1;
    public int IncrementStep { get; init; } = 1;
    public string? ScopeKey { get; init; }
    public string ResetPolicy { get; init; } = "none";
}

public sealed class TemplateSourceBindingDto
{
    public string RegionKind { get; init; } = "paragraph";
    public int ParagraphIndex { get; init; }
    public string? OriginalText { get; init; }
    public int? CharStart { get; init; }
    public int? CharEnd { get; init; }
}

public sealed class DocxStructureDto
{
    public string ResourceId { get; init; } = string.Empty;
    public string? FileName { get; init; }
    public IReadOnlyList<DocxParagraphDto> Paragraphs { get; init; } = Array.Empty<DocxParagraphDto>();
    public int TableCount { get; init; }
}

public sealed class DocxParagraphDto
{
    public int Index { get; init; }
    public string Text { get; init; } = string.Empty;
}
