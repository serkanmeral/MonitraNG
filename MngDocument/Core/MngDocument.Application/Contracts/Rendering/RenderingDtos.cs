namespace MngDocument.Application.Contracts.Rendering;

public sealed class DocumentRenderingStatusDto
{
    public bool Enabled { get; init; }
    public bool GotenbergConfigured { get; init; }
    public bool GotenbergReachable { get; init; }
    public string? GotenbergBaseUrl { get; init; }
    public string? Message { get; init; }
}

public sealed class RenderTemplatePdfRequest
{
    /// <summary>Placeholder anahtar → değer. Boş bırakılırsa tanımlı parametre etiketleri veya anahtar adı kullanılır.</summary>
    public Dictionary<string, string>? Values { get; init; }
}
