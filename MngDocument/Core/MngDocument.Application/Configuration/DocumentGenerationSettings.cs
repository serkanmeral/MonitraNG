namespace MngDocument.Application.Configuration;

public class DocumentGenerationSettings
{
    public List<DocumentGenerationProfileSettings> Profiles { get; set; } = new();
}

public sealed class DocumentGenerationProfileSettings
{
    public string Code { get; set; } = string.Empty;
    public string TemplateCode { get; set; } = string.Empty;
    public string ContextType { get; set; } = string.Empty;
    public List<string> OutputFolderPath { get; set; } = new();
    public string FileNamePattern { get; set; } = "{docNo}.docx";
    public DocumentGenerationIdempotencySettings? Idempotency { get; set; }
    public Dictionary<string, string> Defaults { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class DocumentGenerationIdempotencySettings
{
    public string Dataset { get; set; } = string.Empty;
    public string GuardField { get; set; } = string.Empty;
    public List<string> WritebackFields { get; set; } = new();
}
