namespace MngDocument.Application.Contracts.Generation;

public sealed class GenerateDocumentRequest
{
    public string? ProfileCode { get; set; }
    public string? TemplateCode { get; set; }
    public DocumentGenerationContextDto Context { get; set; } = new();
    public Dictionary<string, string>? Overrides { get; set; }
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
