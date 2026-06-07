using System.Text.Json;

namespace MngNotifier.Application.Models;

public sealed class MailTemplateRecord
{
    public string? TemplateKey { get; set; }
    public string? Subject { get; set; }
    public string? BodyHtml { get; set; }
    public string? LayoutKey { get; set; }
    public string? Locale { get; set; }
    public List<string>? Variables { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class MailLayoutRecord
{
    public string? LayoutKey { get; set; }
    public string? StylesCss { get; set; }
    public string? HeaderHtml { get; set; }
    public string? FooterHtml { get; set; }
    public bool? IsDefault { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class RenderedMailContent
{
    public required string Subject { get; init; }
    public required string HtmlBody { get; init; }
    public required string TemplateKey { get; init; }
    public string? LayoutKey { get; init; }
}

public sealed class TemplateRenderRequest
{
    public required string TemplateKey { get; init; }
    public JsonElement Context { get; init; }
    public string? SubjectOverride { get; init; }
    /// <summary>Preview: render this fragment instead of DG bodyHtml (unsaved draft).</summary>
    public string? BodyHtmlOverride { get; init; }
    /// <summary>Preview: layout key when template not yet in DG or override layout.</summary>
    public string? LayoutKeyOverride { get; init; }
    /// <summary>Preview: template locale for date formatting when record missing or override.</summary>
    public string? LocaleOverride { get; init; }
}
