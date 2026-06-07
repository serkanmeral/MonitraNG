using System.Text.Json;

namespace MngNotifier.Application.DTOs;

public class PreviewTemplateRequest
{
    public required string TemplateKey { get; set; }
    public string? Subject { get; set; }
    public JsonElement Context { get; set; }
}
