namespace MngDocument.Application.Contracts.Generation;

/// <summary>
/// Domain-agnostic runtime input for document producers (manual, API, schedule, OC automation).
/// <see cref="ProducerCode"/> maps to generation profile code until <c>dm_document_producers</c> (G4).
/// </summary>
public sealed class DocumentGenerationRuntimeEnvelope
{
    public string ProducerCode { get; set; } = string.Empty;
    public DocumentGenerationContextDto Context { get; set; } = new();
    public DocumentGenerationScopeDto? Scope { get; set; }
    public Dictionary<string, string>? Params { get; set; }
    public Dictionary<string, string>? Overrides { get; set; }
    public DocumentGenerationTriggerDto? Trigger { get; set; }
    public string? TemplateCode { get; set; }
}

public sealed class DocumentGenerationScopeDto
{
    public string? WorkspaceId { get; set; }
    public string? DomainId { get; set; }
}

public sealed class DocumentGenerationTriggerDto
{
    /// <summary>manual · api · schedule · automation · event</summary>
    public string Kind { get; set; } = "api";

    public string? CorrelationId { get; set; }
}

/// <summary>Optional runtime metadata on <see cref="GenerateDocumentRequest"/>.</summary>
public sealed class DocumentGenerationRuntimeDto
{
    public DocumentGenerationScopeDto? Scope { get; set; }
    public Dictionary<string, string>? Params { get; set; }
    public DocumentGenerationTriggerDto? Trigger { get; set; }
}
