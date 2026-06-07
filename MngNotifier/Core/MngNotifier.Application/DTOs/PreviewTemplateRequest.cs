using System.Text.Json;

namespace MngNotifier.Application.DTOs;

public class PreviewTemplateRequest
{
    public required string TemplateKey { get; set; }
    public string? Subject { get; set; }
    public JsonElement Context { get; set; }
    /// <summary>Optional draft body fragment (preview before save).</summary>
    public string? BodyHtmlOverride { get; set; }
    public string? LayoutKeyOverride { get; set; }
    /// <summary>Template locale for date/time placeholder formatting (e.g. tr, en).</summary>
    public string? LocaleOverride { get; set; }
}
