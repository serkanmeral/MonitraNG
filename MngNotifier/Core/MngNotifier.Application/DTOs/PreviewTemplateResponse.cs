namespace MngNotifier.Application.DTOs;

public class PreviewTemplateResponse
{
    public required string TemplateKey { get; set; }
    public string? LayoutKey { get; set; }
    public required string Subject { get; set; }
    public required string HtmlBody { get; set; }
}
